"""Build a whitelist-only plugin preview. Never read installed/user configuration."""
from pathlib import Path
import hashlib
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "dist/CleanPluginPreview.zip"
PREFIX = "nativePC/plugins/CSharp/WhaleCompanion/"
SOURCES = {
    "WhaleCompanion.dll": "src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.dll",
    "WhaleCompanion.deps.json": "src/WhaleCompanion/bin/Release/net8.0/WhaleCompanion.deps.json",
    "System.Security.Cryptography.ProtectedData.dll": "dist/WhaleCompanion/System.Security.Cryptography.ProtectedData.dll",
    "THIRD-PARTY.md": "THIRD-PARTY.md",
    "LICENSE.ProtectedData.txt": "release/third-party/LICENSE.ProtectedData.txt",
    "NOTICE.ProtectedData.txt": "release/third-party/NOTICE.ProtectedData.txt",
    "LICENSE.MonsterOverlay.txt": "LICENSE.MonsterOverlay.txt",
    "NOTICE.MonsterOverlay.txt": "NOTICE.MonsterOverlay.txt",
    "LICENSE.HunterPie.txt": "dist/WeaponCombatPreview/LICENSE.HunterPie.txt",
    "LICENSE.HunterPieLegacy.txt": "dist/WeaponCombatPreview/LICENSE.HunterPieLegacy.txt",
    "LICENSE.MoreAutoShoutouts.txt": "dist/WhaleCompanion/LICENSE.MoreAutoShoutouts.txt",
    "LICENSE.MhwToolkit.txt": "dist/WhaleCompanion/LICENSE.MhwToolkit.txt",
    "LICENSE.VisualControllerExtended.txt": "dist/WhaleCompanion/LICENSE.VisualControllerExtended.txt",
}
README = """# 大肥鱼插件独立候选包

适用研究目标：MHW/Iceborne 421810 / 15.23，SharpPluginLoader Core 2.0.0。
这是插件候选包，尚非完整模组发行版；不含加载器、原版游戏文件、VRM、模型替换、离线GMD或台词编辑器。
已有整套开发安装请继续使用原有刷新/回滚流程，不要把本包视为整套模型升级。

退出游戏后才可手动部署nativePC目录；先备份现有插件DLL及依赖。
examples目录仅为全新配置示例，不会覆盖config；首次运行可使用代码默认值。
若需配置，在游戏退出后将示例复制至插件config，已有配置请自行合并。
密钥在本机面板设置，包内不含密钥、记忆、聊天、日志或研究采样。

实验战斗功能默认不自动开启；不要把诊断开关当作正式稳定承诺。
怪物详细采样开关为Detailed monster research logging (read-only)，默认关闭。
一分钟输出评价默认关闭，攻击参考需要人工配置，并非完整实时面板。

已实现：武器动作/状态回应、有效回避计数、猫HP恢复、怪物状态、实际旋律增益。
伤害旋律分类与大剑肩撞按用户决定跳过；排队音符不触发演奏回应是正确行为。
玩家实机已覆盖部分武器、有效回避及敌方创生物、猫HP与恢复、若干怪物状态。
其他武器映射、弓/双刀特殊回避、部位破坏输出及新规则仍需最终实测。
单人显示伤害可能混入随从/环境；联网共享给队友看的回复尚未完整验证。
旧退出确认异常未明确归因；最近接任务/重置/退出测试正常。
离线检查与安装哈希不能代替游戏验证。

恢复原DLL及依赖可回退；本包不改动现有配置或模型。
来源和第三方许可见插件目录；未包含研究工具本体。
"""

def build():
    payload = {}
    for name, relative in SOURCES.items():
        source = ROOT / relative
        if source.is_symlink() or not source.is_file() or source.stat().st_size == 0:
            raise ValueError("Missing or indirect release source: " + relative)
        payload[PREFIX + name] = source.read_bytes()
    # Generated public defaults, never copied from the development config.
    payload[PREFIX + "examples/settings.json"] = b"{}\n"
    payload[PREFIX + "examples/persona.md"] = "你是陪玩家狩猎的蓝色鲸鱼娘，聪明、嘴硬但在意同伴。用简短中文自然聊天。\n".encode("utf-8")
    payload["README.md"] = README.encode("utf-8")
    manifest = {name: hashlib.sha256(data).hexdigest().upper() for name, data in sorted(payload.items())}
    payload["SHA256SUMS.json"] = json.dumps(manifest, ensure_ascii=False, indent=2).encode("utf-8")
    temporary = OUT.with_suffix(".zip.tmp")
    with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED) as bundle:
        for name, data in sorted(payload.items()):
            entry = zipfile.ZipInfo(name, date_time=(2026, 10, 27, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            bundle.writestr(entry, data)
    with zipfile.ZipFile(temporary) as bundle:
        if set(bundle.namelist()) != set(payload) or bundle.testzip() is not None:
            raise ValueError("Archive contents mismatch")
        for name, expected in manifest.items():
            if hashlib.sha256(bundle.read(name)).hexdigest().upper() != expected:
                raise ValueError("Archive hash mismatch")
    temporary.replace(OUT)
    print(json.dumps({"archive": str(OUT), "files": len(payload), "user_config_read": False, "installed": False}))

if __name__ == "__main__":
    build()
