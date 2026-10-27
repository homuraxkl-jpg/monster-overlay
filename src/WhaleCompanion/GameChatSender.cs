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

// Field layout reference: eigeen/mhw-toolkit game_util.rs/game_export.rs.
// The SPL-resolved GUI and uGUIChat must agree before any writes are attempted.
public static class GameChatSender
{
    public static ChatSendResult TrySend(IChatMemory memory,ulong gui,ulong chat,string text,out string status,bool checkOnly=false)
    {
        status="发送界面未就绪";
        if(!LocalProcessMemory.IsAddress(gui,0x1474C)){status="sMhGUI不可用";return ChatSendResult.Unavailable;}
        if(!LocalProcessMemory.IsAddress(chat,0x325F)){status="未找到活动uGUIChat对象";return ChatSendResult.Unavailable;}
        if(!memory.Read(gui+0x13FD0,8,out var pointer)||BitConverter.ToUInt64(pointer)!=chat){status="聊天对象布局未通过校验";return ChatSendResult.Unavailable;}
        if(!memory.Read(gui+0x14748,4,out var targetBytes)){status="无法读取当前频道";return ChatSendResult.Unavailable;}
        var target=BitConverter.ToInt32(targetBytes);
        if(target is not (0 or 1 or 3 or 4)){status=target==2?"当前为私聊频道，暂不共享":"当前频道未验证";return ChatSendResult.Unavailable;}
        if(!memory.Read(chat+0x2A58,4,out var visible)||!memory.Read(chat+0x325E,1,out var flag)||!memory.Read(chat+0x2A5D,128,out var original))return ChatSendResult.Unavailable;
        var inputVisible=BitConverter.ToInt32(visible);
        if(inputVisible is not (0 or 1)||flag[0]>1){status="聊天输入状态布局未验证";return ChatSendResult.Unavailable;}
        if(inputVisible==1||flag[0]!=0||original[0]!=0){status="等待你结束输入或游戏发送完毕";return ChatSendResult.Busy;}
        if(checkOnly){status=$"发送器检查通过，当前频道={target}（未发送）";return ChatSendResult.Submitted;}
        var encoded=Encoding.UTF8.GetBytes(text);
        if(encoded.Length is 0 or >127||encoded.Contains((byte)0)||!SharedChat.IsCompanion(text)){status="共享文字格式不合法";return ChatSendResult.Unavailable;}
        var buffer=new byte[128];encoded.CopyTo(buffer,0);
        if(!memory.Write(chat+0x2A5D,buffer)){status="无法写入聊天输入缓冲区";return ChatSendResult.Unavailable;}
        if(!memory.Write(chat+0x325E,[1])){memory.Write(chat+0x2A5D,original);status="发送标记写入失败，已尝试恢复输入";return ChatSendResult.Unavailable;}
        status=$"已交给游戏发送，频道={target}（未确认队友收到）";return ChatSendResult.Submitted;
    }
}

