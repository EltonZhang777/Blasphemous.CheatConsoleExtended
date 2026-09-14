# Tab completion uses the existing console input boundary

status: accepted

Tab 命令补全接入原版控制台已有的输入处理边界，并复用第 1 组的严格命令目录；本 mod 只在补全候选菜单需要时处理 Tab、Escape 和上下方向键，Enter 继续走原版 Submit。这样可以保留严格命令解析、命令执行优先级、输入焦点和历史命令行为，同时避免建立第二套命令执行器。
