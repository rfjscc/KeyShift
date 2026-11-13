# KeyShift · 键盘布局切换器

一个不需要安装的小工具：修改注册表，可以让你使用英式布局打中文用一次点击，在 **美式键盘（`KBDUS.DLL`）** 与 **英式键盘（`KBDUK.DLL`）** 之间切换 Microsoft 拼音所使用的“简体中文 - 美式键盘”布局（注册表 ID `00000804`）。

![KeyShift 界面预览](dist/界面预览.png)

## 直接使用

1. 双击 `dist\KeyShift.exe`。
2. 程序会先以普通权限启动，并读出当前布局；点击右侧的“立即切换为英式键盘 / 美式键盘”。
3. Windows 弹出 UAC 提示时选择“是”。
4. 新窗口会完成写入并显示结果。**注销或重启 Windows 后生效**（程序不会自动注销或重启电脑）。

如果程序已经以管理员身份运行，点击后会直接切换，不再弹 UAC。

## 它修改什么

| 注册表值 | 行为 |
| --- | --- |
| `Layout File` | 在 `KBDUS.DLL` 和 `KBDUK.DLL` 之间切换 |
| `Layout Text` | 识别到 `US Keyboard` / `UK Keyboard` 或 `美式键盘` / `英式键盘` 时同步更新；自定义名称保持原样 |

位置：

```text
HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\00000804
```

**不会**修改 `Layout Display Name`，不会替换或下载任何 `.DLL`，不会安装服务/驱动，不会创建开机启动项，也不联网。

写入前会校验目标 DLL 是否存在于 `System32`。如果写入中途出现异常，会尝试回滚原来的值。

## 恢复默认

- 在程序中点击“恢复系统默认”，再点一次“确认恢复”。这会把 `Layout File` 写回 `KBDUS.DLL`，并在可识别时把名称还原为美式。
- 也可以手动在注册表中把 `Layout File` 改为 `KBDUS.DLL`、`Layout Text` 改为 `Chinese (Simplified) - US Keyboard`。
- 如果当前已是美式布局，“恢复系统默认”会置灰。

## 为什么需要注销或重启

Windows 在登录时加载并缓存键盘布局 DLL。注册表写入后，当前登录会话仍可能继续使用旧的布局，因此需要重新登录或重启。程序不会执行自动注销/重启，避免误丢失未保存的工作。

## 权限说明

- 主窗口本身以普通权限运行，便于预览状态。
- 只有点击切换按钮时才会请求管理员权限（UAC）。
- 由于程序未做商业代码签名，UAC 中可能显示“未知发布者”；源码就在 `src`，可以自行审查和重新编译。

## 常见问题

**界面右上角显示“标准权限”，正常吗？**  
正常。点击切换时才会申请管理员权限。如果程序已提权，会显示“管理员”。

**点击后没有变化？**  
请确认 UAC 中点了“是”。取消授权不会写入任何内容。

**切换成功但键位没变？**  
请先注销并重新登录，或重启。这个工具按你提供的方法只修改 `00000804` 这一项，不会主动切换当前输入法；如果你的拼音使用的是其他布局编号，需要确认语言设置。

**会修改系统文件吗？**  
不会。只改上述两个注册表字符串值。建议先关闭正在编辑的文档再重启，以防丢失未保存内容。

**支持哪些系统？**  
Windows 10 / 11（64 位优先）。界面依赖 .NET Framework 4.8，Windows 10 1903+ 和 Windows 11 已内置。

## 构建

源码为 C# / WPF，4 个源文件，无需安装 Visual Studio 或 .NET SDK；使用 Windows 自带的 .NET Framework 编译器即可：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

输出：`dist\KeyShift.exe`。

预览构建（包含截图参数，仅用于开发界面）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Preview
```

预览构建参数：`--preview=uk|us|unknown --screenshot=<png路径>`。正式版不包含这些参数。

## 文件结构

```text
keyshift/
├─ src/                 C# / WPF 源码
├─ assets/             应用图标与图标生成脚本
├─ tests/              注册表值转换的轻量测试
├─ preview/            界面预览截图
├─ dist/
│  ├─ KeyShift.exe     可直接运行
│  ├─ 使用说明.txt
│  └─ 界面预览.png
├─ app.manifest        权限、DPI、Windows 版本声明
└─ build.ps1           一键构建脚本
```

## 说明

本项目按用户给定的注册表方案实现，属于系统级修改。每个用户的环境不同，建议先确认当前布局状态，再在方便注销/重启的时间操作。
