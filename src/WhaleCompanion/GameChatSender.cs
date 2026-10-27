using System.Runtime.InteropServices;
using System.Text;

namespace WhaleCompanion;

public interface IChatMemory
{
    bool Read(ulong address,int count,out byte[] bytes);
    bool Write(ulong address,byte[] bytes);
}

public sealed class SelfChatMemory : IChatMemory
{
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process,nint address,[Out] byte[] bytes,nuint count,out nuint read);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(nint process,nint address,byte[] bytes,nuint count,out nuint written);
    private static bool Valid(ulong address,int size)=>size>0&&size<=128&&LocalProcessMemory.IsAddress(address,(uint)size);
    public bool Read(ulong address,int count,out byte[] bytes)
    {
        bytes=[];if(!Valid(address,count))return false;bytes=new byte[count];
        return ReadProcessMemory(-1,(nint)address,bytes,(nuint)count,out var copied)&&copied==(nuint)count;
    }
    public bool Write(ulong address,byte[] bytes)=>Valid(address,bytes.Length)&&WriteProcessMemory(-1,(nint)address,bytes,(nuint)bytes.Length,out var copied)&&copied==(nuint)bytes.Length;
}

public enum ChatSendResult { Submitted, Busy, Unavailable }

// Keep only a few already-sent buffers in RAM. Unknown drafts are never considered consumed.
public sealed class ChatInputHistory
{
    private readonly object gate=new();
    private ulong gui,chat;
    private readonly Queue<byte[]> sent=new();
    public void Reset(){lock(gate){gui=chat=0;sent.Clear();}}
    public void Bind(ulong root,ulong widget)
    {
        lock(gate){if(gui!=root||chat!=widget){sent.Clear();gui=root;chat=widget;}}
    }
    public void ConfirmSent(string text)
    {
        if(string.IsNullOrWhiteSpace(text))return;var bytes=Encoding.UTF8.GetBytes(text);
        if(bytes.Length>127||bytes.Contains((byte)0))return;
        var buffer=new byte[128];bytes.CopyTo(buffer,0);
        lock(gate){if(gui==0||chat==0)return;while(sent.Count>=4)sent.Dequeue();sent.Enqueue(buffer);}
    }
    public bool CanReuse(ulong root,ulong widget,byte[] buffer)
    {
        int length=Array.IndexOf(buffer,(byte)0);if(length<0)return false;
        lock(gate)return gui==root&&chat==widget&&sent.Any(x=>length<x.Length&&x[length]==0&&x.AsSpan(0,length).SequenceEqual(buffer.AsSpan(0,length)));
    }
}

// Field layout reference: eigeen/mhw-toolkit game_util.rs/game_export.rs.
// The SPL-resolved GUI and uGUIChat must agree before any writes are attempted.
public static class GameChatSender
{
    public static ChatSendResult TrySend(IChatMemory memory,ulong gui,ulong chat,string text,out string status,bool checkOnly=false,ChatInputHistory? history=null)
    {
        status="发送界面未就绪";
        if(!LocalProcessMemory.IsAddress(gui,0x1474C)){status="sMhGUI不可用";return ChatSendResult.Unavailable;}
        if(!LocalProcessMemory.IsAddress(chat,0x325F)){status="未找到活动uGUIChat对象";return ChatSendResult.Unavailable;}
        if(!memory.Read(gui+0x13FD0,8,out var pointer)||BitConverter.ToUInt64(pointer)!=chat){status="聊天对象布局未通过校验";return ChatSendResult.Unavailable;}
        if(!memory.Read(gui+0x14748,4,out var targetBytes)){status="无法读取当前频道";return ChatSendResult.Unavailable;}
        var target=BitConverter.ToInt32(targetBytes);
        if(target is not (0 or 1 or 3 or 4)){status=target==2?"当前为私聊频道，暂不共享":"当前频道未验证";return target==2?ChatSendResult.Busy:ChatSendResult.Unavailable;}
        if(!memory.Read(chat+0x2A58,4,out var visible)||!memory.Read(chat+0x325E,1,out var flag)||!memory.Read(chat+0x2A5D,128,out var original))return ChatSendResult.Unavailable;
        var inputVisible=BitConverter.ToInt32(visible);
        if(inputVisible is not (0 or 1)||flag[0]>1){status="聊天输入状态布局未验证";return ChatSendResult.Unavailable;}
        if(inputVisible==1){status="Chat input is open; waiting for you to finish";return ChatSendResult.Busy;}
        if(flag[0]!=0){status="Previous game chat send is still pending";return ChatSendResult.Busy;}
        if(original[0]!=0&&history?.CanReuse(gui,chat,original)!=true){status="Closed chat retains an unconfirmed draft; not overwritten";return ChatSendResult.Busy;}
        if(checkOnly){status=$"发送器检查通过，当前频道={target}（未发送）";return ChatSendResult.Submitted;}
        var encoded=Encoding.UTF8.GetBytes(text);
        if(encoded.Length is 0 or >127||encoded.Contains((byte)0)||!SharedChat.IsCompanion(text)){status="共享文字格式不合法";return ChatSendResult.Unavailable;}
        var buffer=new byte[128];encoded.CopyTo(buffer,0);
        // Recheck the root and idle bytes immediately before writing, so a newly opened input is left alone.
        if(!memory.Read(gui+0x13FD0,8,out var currentRoot)||BitConverter.ToUInt64(currentRoot)!=chat||
            !memory.Read(gui+0x14748,4,out var currentTarget)||BitConverter.ToInt32(currentTarget)!=target||
            !memory.Read(chat+0x2A58,4,out var currentVisible)||BitConverter.ToInt32(currentVisible)!=0||
            !memory.Read(chat+0x325E,1,out var currentFlag)||currentFlag[0]!=0||
            !memory.Read(chat+0x2A5D,128,out var currentBuffer)||!currentBuffer.AsSpan().SequenceEqual(original))
        {status="Chat input changed during validation; retrying without writes";return ChatSendResult.Busy;}
        if(!memory.Write(chat+0x2A5D,buffer)){status="无法写入聊天输入缓冲区";return ChatSendResult.Unavailable;}
        if(!memory.Write(chat+0x325E,[1])){memory.Write(chat+0x2A5D,original);status="发送标记写入失败，已尝试恢复输入";return ChatSendResult.Unavailable;}
        history?.ConfirmSent(text);
        status=$"已交给游戏发送，频道={target}（未确认队友收到）";return ChatSendResult.Submitted;
    }
}

