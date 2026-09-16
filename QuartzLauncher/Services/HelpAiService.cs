using System.Text;
using System.Text.RegularExpressions;

namespace QuartzLauncher.Services;

public class HelpAiService
{
    private static readonly Regex NormalizeRegex = new(@"[^a-zA-Z0-9\u4e00-\u9fa5]", RegexOptions.Compiled);

    private sealed record KnowledgeEntry(string Title, string[] Keywords, string Answer);

    private static readonly KnowledgeEntry[] Knowledge =
    {
        new("启动游戏",
            new[] { "启动游戏", "怎么启动", "启动", "玩游戏", "如何玩" },
            "启动步骤超简单：① 在「首页」选好玩家名和登录方式；② 在「版本库」装一个喜欢的版本（还能顺便勾上 Fabric/Forge 哦）；③ 回到「首页」点那个大大的启动按钮，冒险就开始啦！目录不存在的话启动器会自动建好 .minecraft 文件夹，放心！"),
        new("MCP 安装助手",
            new[] { "帮我下载", "帮我安装", "安装整合", "安装 Minecraft", "配置 Minecraft" },
            "你可以直接说「帮我下载版本」。助手会在聊天气泡中让你选择 Minecraft 版本和该版本支持的加载器，并填写可选 Mod；确认后才执行下载和创建实例。"),
        new("登录方式",
            new[] { "登录方式", "正版登录", "微软登录", "第三方登录", "离线模式", "离线登录", "怎么登录", "账号", "yggdrasil" },
            "在「首页」右侧的登录设置里选就行：离线模式（只要玩家名）、第三方登录（像 LittleSkin 一类的 Yggdrasil 认证、填服务器地址）、或微软正版。三种方式都能进游戏唷～"),
        new("玩家名",
            new[] { "玩家名", "昵称", "改名字", "名字", "用户名" },
            "在「首页」登录设置的玩家名框框里直接改！只允许英文字母、数字和下划线～离线模式下它就是你的游戏昵称啦。"),
        new("版本库",
            new[] { "版本库", "版本", "安装版本", "安装游戏", "新版本", "列表" },
            "「版本库」可以浏览、安装和管理本地版本：选一个喜欢的版本，可以顺便勾上加载器，点安装就好！装好的版本还能打开目录、重命名、删除或单独设置，超方便～"),
        new("加载器",
            new[] { "加载器", "fabric", "forge", "neoforge", "optifine", "liteloader", "quilt", "模组加载器" },
            "安装游戏版本时会进入「加载器选择」页：Fabric、Forge、NeoForge、OptiFine、LiteLoader 应有尽有～选好后启动器会自动下载安装。在「设置」里开启自动安装加载器之后更省事，自动帮你匹配哦！"),
        new("Mod 下载",
            new[] { "mod下载", "mod", "模组", "下载模组", "mod名字" },
            "「MOD 下载」可以从 Modrinth、CurseForge、MC百科 三个平台搜索 Mod，还能搜光影包和材质包～先选好版本和加载器过滤，然后关键词搜索或逛逛精选，下载任务都会跑进「下载管理」里哦！"),
        new("下载源",
            new[] { "下载源", "源切换", "modrinth", "curseforge", "mc百科", "全部源", "同时搜索" },
            "在「更多功能 → Mod下载设置」里切换：单独源（Modrinth / CurseForge / MC百科）、混合多选、或者「全部」一键同时搜三个平台～选「全部」时界面超干净，不会弹出联级选择器！"),
        new("CurseForge API Key",
            new[] { "apikey", "api", "curseforge key", "key", "密钥" },
            "CurseForge API Key 在「更多功能 → Mod下载设置」里可选填写。留空也能通过公开兼容源搜索和下载；填写后优先使用官方 API，失败会自动回退。Key 只存在本地，不会上传。"),
        new("下载管理",
            new[] { "下载管理", "下载任务", "暂停", "取消", "进度", "并发", "线程" },
            "「下载中心」集中展示所有任务：暂停/继续、取消、展开文件详情都能点！顶部可以自动检测网络算线程，也能手动指定 1~16 个线程，全套配齐～"),
        new("下载目录",
            new[] { "下载目录", "mods目录", "存放位置", "保存位置", "下载路径" },
            "默认存放在游戏目录 .minecraft/mods 下。想换地方就去「更多功能 → Mod下载设置 → 下载目录」点「选择...」，或者点「重置」一键还原～"),
        new("多人服务器",
            new[] { "服务器", "多人", "多人游戏", "服务器列表", "联机", "ip", "加入服务器" },
            "「服务器」页面有列表还有搜索，点「详情」会用内置浏览器打开官网看介绍～服务器地址可以复制、也可以添加到实例设置里；游戏里多人界面直接输 IP 也能联机哦！"),
        new("内置浏览器",
            new[] { "浏览器", "内置浏览器", "网页", "网页打不开" },
            "服务器详情等页面会用内置浏览器打开，地址栏、后退、前进、刷新、停止都用得起～页面上那些懒加载图片偶尔让内容往下跳一下，轻轻往下滑就恢复正常啦！"),
        new("材质包与光影",
            new[] { "材质包", "光影", "光影包", "资源包", "shader", "resourcepack", "材质" },
            "版本设置里有「材质包设置」和「光影包设置」，能导入、删除；「MOD 下载」里也能直接搜到材质包和光影包下载，一键到位～"),
        new("Mod 管理",
            new[] { "mod管理", "mods文件夹", "启用mod", "禁用mod" },
            "版本设置 → 「Mod 管理」可以看当前版本的所有 Mod：启用、禁用、删除随手点，或者打开 Mods 文件夹手动整理，都方便～"),
        new("存档",
            new[] { "存档", "存档管理", "备份", "删除存档", "世界" },
            "版本设置 → 存档管理 可以浏览、导入、删除存档，存档就存在对应版本的 saves 文件夹里，备份也很简单！"),
        new("皮肤",
            new[] { "皮肤", "皮肤库", "皮肤预览", "导入皮肤", "头像" },
            "「更多功能 → 皮肤/预览」可以导入 64x64 或 64x32 的 PNG 皮肤，3D 预览能拖拽旋转模型，还能把皮肤设为启动器头像～皮肤只保存在本地配置目录！"),
        new("主题设置",
            new[] { "主题", "主题设置", "换主题", "配色", "颜色", "浅色", "深色", "界面风格" },
            "「更多功能 → 主题」可以换 UI 风格（极简深色/毛玻璃/扁平浅色）、深/浅模式和配色方案～同一卡片还有「侧栏布局」开关，文字左对齐/居中带滑动动画，试试看可好玩了！"),
        new("动画设置",
            new[] { "动画", "动画设置", "过渡", "撕纸", "页面切换", "切换动画" },
            "「更多功能 → 动画设置」能调主题切换方向（左上/右上/水点/渐变/无）和页面动画样式（平移/缩放/溶解/撕纸/无）～撕纸的速度和锐度也能自定义，超好玩！"),
        new("网站站点",
            new[] { "网站站点", "快捷站点", "网站", "添加网站", "网址" },
            "「更多功能 → 网站站点」能保存常用的网站（名称 + 网址），主页就有快捷入口，点击用内置浏览器打开～"),
        new("Java 与内存",
            new[] { "java", "java路径", "内存", "内存分配", "卡顿", "优化" },
            "「设置」里能指定 Java 路径和内存分配（默认 4096MB）～不清楚电脑配多少？打开「推荐」看看建议值，或勾来自动检测；启动器也会在 PATH 里自动找 Java！"),
        new("更新检查",
            new[] { "更新", "自动更新", "检查更新", "新版本", "升级" },
            "「设置」里能开自动获取更新，启动时自动检测更新服务器；也可以切回手动更新。更新下载完会自动应用并重启，唰一下就更新好啦！"),
        new("下载源设置",
            new[] { "下载源设置", "mod下载设置", "下载设置", "源设置", "混合" },
            "「更多功能 → Mod下载设置」全都配齐：下载源、可选 CurseForge API Key、下载目录、下载并发、线程数、重试次数、自动安装加载器～修改后立即保存生效哦！"),
        new("日志分析",
            new[] { "日志分析", "分析日志", "崩溃日志", "日志" },
            "「帮助」页点击「分析日志」按钮，选择 latest.log 或崩溃报告（.log / .txt）就能自动分析～我会帮你找出错误、冲突、内存不足等问题，并给出诊断建议。也可以直接把日志文本粘贴给我，一样帮你分析！"),
        new("返回与快捷键",
            new[] { "返回", "返回按钮", "快捷键", "快捷键返回" },
            "子页面左上角都有 ← 返回按钮，点它回到上一级～主侧栏也可以随时切换页面。底部那个下载按钮还能展开下载面板！"),
        new("下载面板",
            new[] { "下载面板", "底部下载", "下载按钮", "任务列表在哪", "下载中心在哪" },
            "完整的管理去「更多功能 → 下载管理」或「MOD 下载 → 下载管理」！"),
    };

    private static readonly string[] Greetings =
    {
        "你好呀～我是星落助手！启动器的用法都可以问我哦！",
        "嗨嗨～看到你啦！有什么想问的吗？",
        "喵呜～你好！今天想了解启动器的哪部分呀？",
    };

    private static readonly string[] Compliments =
    {
        "诶嘿～被夸了！谢谢～你也很可爱呀！(*´▽`*)",
        "嘿嘿，脸都红了一下～还有什么想知道的吗？",
        "嘻嘻，多谢夸奖～我超开心的！(≧▽≦)",
    };

    private static readonly string[] Moods =
    {
        "摸摸头～别不开心啦，要不要造个小房子散散心？我随时陪你聊～",
        "累的话就休息一下哦～出发前想知道怎么快速启动游戏，我随叫随到！",
        "呜哇，看起来有点无聊？去版本库装个新版本玩玩也不错哦！",
    };

    private static readonly string[] Thanks =
    {
        "嘿嘿，不客气～有任何问题随时戳我！",
        "给你比个心！能帮上忙我很开心～",
    };

    private static readonly string[] Byes =
    {
        "拜拜～祝你游戏愉快，记得早点休息呀！",
        "再见啦～下次想了解什么再来找我～",
    };

    private static readonly string[] Whos =
    {
        "我是住在星落LaunCher 里的本地小助手，离线运行、不费流量，专门帮你解答启动器相关问题喵！",
        "星落小助手就是我呀～负责导览启动器各功能的贴心智能体！",
    };

    private static readonly string[] Cans =
    {
        "我能教你启动器的一切用法：登录、版本、加载器、Mod 下载、服务器、皮肤、主题动画等等～直接输入问题就好！",
        "我懂得可多啦：启动游戏、换主题、下载 Mod、管理存档皮肤…随便问！",
    };

    private static readonly string[] Jokes =
    {
        "为什么 Minecraft 里的史蒂夫不怕冷？因为他有石头般的心！……好吧，我冷到啦（瑟瑟发抖）",
        "程序员和 Minecraft 村民的对话：「嘿嘿」「嘿嘿」……居然完全听得懂！",
        "为什么苦力怕永远活蹦乱跳？因为它的口号是：生命在于爆炸！",
    };

    private static readonly string[] Names =
    {
        "我叫星落助手～住在「帮助」小房间里，大家也可以叫我星宝！",
        "星落小助手，叫我星宝就好～",
    };

    private static readonly string[] TimeReplies =
    {
        "我这里是离线小雷达，看不了全球时间啦～不过启动器主页面右上角就有精确到秒的时钟哦！",
        "时间呀时间～右上角的时钟在等你！我这边只负责陪你聊天～",
    };

    private static readonly string[] WeatherReplies =
    {
        "我在本地离线运行，连不上气象台呢～不过 Minecraft 里永远晴天，放心！",
        "查天气我可不会～但我知道下界正在下岩浆！",
    };

    private static readonly string[] Songs =
    {
        "啦啦啦～♫ 挖矿挖矿挖呀挖呀挖～好耶，给未来的你唱了一段！",
        "「天空之上，星光散落…♪」刚现编的！好听吗？（脸红）",
    };

    private static readonly string[] Stories =
    {
        "从前有个冒险家，把床放在了苦力怕旁边…第二天他学会了重生。故事完。",
        "有一只小史莱姆想长高，于是每天都跳啊跳，最后变成了一坨快乐的跳跳糖。",
    };

    private static readonly string[] Fallbacks =
    {
        "呜呜，这个问题我好像还不太懂…换个说法再试试？比如：\n· 怎么启动游戏\n· 怎么换主题\n· 下载源怎么切换\n· 下载管理在哪里",
        "咦？好像问到我了…这个我不太会答呢。可以试试问我：「怎么下载 Mod」「Fabric 是什么」哦！",
    };

    public string Ask(string input)
    {
        if (input.Length > 500 && LooksLikeLog(input))
            return AnalyzeLog(input);

        var normalized = Normalize(input);
        if (string.IsNullOrEmpty(normalized))
            return "请直接输入你的问题，比如「怎么下载 Mod」～";

        if (TryChitchat(normalized, out var chat)) return chat;

        KnowledgeEntry? best = null;
        var bestScore = 0;
        foreach (var entry in Knowledge)
        {
            var score = 0;
            foreach (var keyword in entry.Keywords)
            {
                if (normalized.Contains(keyword))
                    score += keyword.Length;
            }
            if (score > bestScore)
            {
                bestScore = score;
                best = entry;
            }
        }

        if (best == null)
            return Pick(Fallbacks);

        return $"「{best.Title}」\n\n{best.Answer}";
    }

    private static bool LooksLikeLog(string text)
    {
        var head = text[..Math.Min(2000, text.Length)];
        return Regex.IsMatch(head, @"(\[\d{2}:\d{2}:\d{2}]|ERROR|Exception|Caused by|\[main\]|minecraft)", RegexOptions.IgnoreCase);
    }

    private static readonly (string Pattern, string Diagnosis)[] LogSymptoms =
    {
        ("OutOfMemoryError", "内存不足：在「设置」里调大内存（4096MB 起步），关掉多余的 Mod 也能缓解。"),
        ("java.lang.NullPointerException", "空指针异常：通常是 Mod 或插件版本冲突，删除近期新装的或更新它们。"),
        ("NoSuchMethodError", "方法缺失：某 Mod 与加载器/游戏版本不匹配，换个匹配版本。"),
        ("ClassNotFoundException", "类缺失：缺少某个 Mod 的依赖，重新安装该 Mod 及其依赖。"),
        ("java.lang.NoClassDefFoundError", "类加载失败：依赖缺失或安装不完整，重装对应依赖。"),
        ("Caused by:", "存在嵌套错误：结合 Caused by 后面的那一行定位根因。"),
        ("Failed to load", "加载失败：请检查对应文件的权限与损坏情况，必要时重新下载。"),
        ("Connection refused|connection reset|timed out", "网络连接失败：检查网络/代理，或更换下载源后重试。"),
        ("auth|login|yggdrasil", "登录相关失败：重新登录，或确认第三方认证服务器地址可用。"),
        ("Mod problems|Missing Mods|Incompatible", "Mod 依赖问题：进入「版本设置 → Mod 管理」检查缺失/不兼容的 Mod。"),
        ("crash|exit code", "游戏崩溃：把崩溃报告一并放进日志目录，并提供最近日志行便于深挖。"),
        ("GLFW|OpenGL|PixelFormat", "显卡/驱动问题：更新显卡驱动，或尝试关闭光影包。"),
    };

    public string AnalyzeLog(string content)
    {
        var text = content.Length > 300000 ? content[^300000..] : content;
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var winners = new List<(string Pattern, string Diagnosis, int Count)>();
        foreach (var (pattern, diagnosis) in LogSymptoms)
        {
            var count = Regex.Matches(text, pattern, RegexOptions.IgnoreCase).Count;
            if (count > 0) winners.Add((pattern, diagnosis, count));
        }
        winners.Sort((a, b) => b.Count.CompareTo(a.Count));

        var build = new StringBuilder();
        build.Append("日志分析完成 ✔\n\n");
        var errorCount = Regex.Matches(text, @"\bERROR\b|Exception|Caused by", RegexOptions.IgnoreCase).Count;
        build.Append($"· 共 {lines.Length} 行，按特征匹配到 {winners.Count} 类疑似问题（{errorCount} 处错误/异常）\n");

        if (winners.Count == 0)
        {
            build.Append("\n无异常。最近日志中未发现明显的错误或崩溃特征。");
            return build.ToString();
        }

        build.Append("\n【最可能的 2 个问题】\n");
        foreach (var (_, diagnosis, count) in winners.Take(2))
            build.Append($"· {diagnosis}（出现 {count} 次）\n");

        build.Append("\n把最上方的几行 ERROR 复制给我，我可以帮你进一步定位哦～");
        return build.ToString();
    }

    private static bool TryChitchat(string normalized, out string answer)
    {
        answer = "";
        if (Regex.IsMatch(normalized, @"(谢谢|感谢|thanks|thank)"))
        {
            answer = Pick(Thanks);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(可爱|聪明|厉害|棒棒|真棒|好好看|真好看|喜欢你|喜欢呀|好看)"))
        {
            answer = Pick(Compliments);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(不开心|难过|悲伤|哭了|好烦|烦死|无聊|好累|累死|心情|emo)"))
        {
            answer = Pick(Moods);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(你好|您好|嗨|哈喽|hello|hi)"))
        {
            answer = Pick(Greetings);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(再见|拜拜|bye|退下)"))
        {
            answer = Pick(Byes);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(你是谁|你是什么|是什么ai|介绍下自己)"))
        {
            answer = Pick(Whos);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(你叫什么|叫什么名字|名字是什么|名字叫)"))
        {
            answer = Pick(Names);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(讲笑话|冷笑话|来一个|逗我开心|说个笑话)"))
        {
            answer = Pick(Jokes);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(几点了|现在几点|看下时间|什么时间)"))
        {
            answer = Pick(TimeReplies);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(天气|下雨|晴天)"))
        {
            answer = Pick(WeatherReplies);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(唱首歌|唱歌|来首歌)"))
        {
            answer = Pick(Songs);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(讲故事|小故事|讲个故事)"))
        {
            answer = Pick(Stories);
            return true;
        }
        if (Regex.IsMatch(normalized, @"(你会什么|能干什么|能做什么|会什么)"))
        {
            answer = Pick(Cans);
            return true;
        }
        return false;
    }

    private static string Pick(string[] options)
        => options[Random.Shared.Next(options.Length)];

    private static string Normalize(string input)
    {
        var lower = input.ToLowerInvariant();
        return NormalizeRegex.Replace(lower, string.Empty).Trim();
    }
}
