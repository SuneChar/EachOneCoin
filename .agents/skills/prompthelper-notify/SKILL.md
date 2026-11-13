---
name: prompthelper-notify
description: 使用本机 PromptHelper 后台程序发送任务完成、失败或等待处理的微信通知。在用户要求通过 PromptHelper 通知时使用。
---

# PromptHelper 通知

在用户授权通知的任务完成、失败或需要用户处理时，选择对应状态并发送一条简短消息：

```powershell
prompthelper notify --status completed --title "简短标题" --body "简短摘要"
```

## 消息格式

标题格式为 `【项目名】事件`；项目名不明确时直接写事件。正文使用下列字段，只填写已知且与本次任务有关的内容，省略不适用的字段；每条合并为一条消息。

- **完成**（`completed`）：标题 `【项目名】任务完成`；正文 `结果：做了什么\n验证：通过、失败或未运行的检查\n后续：需要用户做什么（如有）`。
- **失败或受阻**（`failed`）：标题 `【项目名】任务受阻`；正文 `问题：失败现象或阻碍\n影响：哪些工作尚未完成\n后续：建议的下一步`。
- **等待用户**（`waiting`）：标题 `【项目名】需要你处理`；正文 `事项：需要决定或提供什么\n原因：为什么需要\n下一步：如何回复或处理`。需要确认时，明确要确认的具体内容，不假设用户已经同意。
- **重要进展**（`info`）：标题 `【项目名】任务进展`；正文 `进展：已完成的关键步骤\n当前：正在做什么或还剩什么`。仅在用户要求进度通知或出现值得通知的变化时发送，避免逐步刷屏。

正文使用真实换行；PowerShell 中可使用双引号和 `` `n ``。例如：

```powershell
prompthelper notify --status completed --title '【PromptHelper】任务完成' --body "结果：已加入消息状态提示`n验证：构建通过；手机收发待验证"
```

标题限 100 字，正文限 4000 字；优先用几句话说明关键内容。不要编造测试结果或将未验证的工作写成已完成。仅发送必要摘要，不发送密码、密钥、源码或完整日志。调用命令时正确转义动态内容，避免把消息内容当作 PowerShell 代码执行。

## 结果处理

退出码 0 表示请求已提交且微信没有明确拒绝，不代表送达；命令输出会说明是否收到确认状态。非零表示被拒绝或结果无法确认；通知结果不改变任务本身的结果。

微信显示可能延迟；需要确认送达时，可以等待 5–10 秒，再请用户查看手机微信是否收到。等待或接口响应均不能证明消息已送达，不要因短暂延迟重复发送。

## 启动与故障处理

此电脑当前构建目录是 `D:\_Projects\PromptHelper\artifacts\app`。如果 `prompthelper` 不在 PATH 中，直接使用完整路径调用命令行程序：

```powershell
& 'D:\_Projects\PromptHelper\artifacts\app\prompthelper.exe' notify --status completed --title '标题' --body '正文'
```

可用 `Get-Process -Name 'PromptHelper.Tray' -ErrorAction SilentlyContinue | Select-Object Id,Path` 检查进程；确认本项目的后台程序没有运行时，在同一个 Windows 用户会话中启动托盘程序。检查结果受执行环境权限影响，不能查看进程时不要假定它未运行。启动命令本身不会发送微信消息：

```powershell
Start-Process -FilePath 'D:\_Projects\PromptHelper\artifacts\app\PromptHelper.Tray.exe' -ArgumentList '--background' -WindowStyle Hidden
```

启动后等待几秒让后台初始化，再调用通知命令。已运行时不必重复启动。如果程序已移动，使用实际构建目录；如果文件不存在，说明需要检查安装或构建，告知用户，不要擅自重新构建或索取凭据。

- **命令不存在**：改用上面的完整路径；仍不存在时报告实际情况。
- **后台未运行**：启动后再发送。若此前的发送尝试已经执行，只有能确定消息未发出时才重试一次；否则先请用户查看微信，因为命令的连接或响应超时可能发生在消息已发出之后。
- **未绑定或会话不可用**：提示用户在托盘设置中完成扫码，或先在微信 ClawBot 中发一条消息；绑定需用户自行操作。
- **微信拒绝、网络错误或超时**：报告命令返回的结果；超时和断线可能仍已送达，不自动重试，等待用户核实。
