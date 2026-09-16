namespace QuartzLauncher.Models;

public class Theme
{
    public string Name { get; set; } = "";
    public string Primary { get; set; } = "#2B86E5";
    public string Bg { get; set; } = "#E8EEF4";
    public string Card { get; set; } = "#FFFFFF";
    public string Text { get; set; } = "#1E293B";
    public string TextMuted { get; set; } = "#94A3B8";
    public string Sidebar { get; set; } = "#FFFFFF";
    public string Border { get; set; } = "#E2E8F0";
    public string Danger { get; set; } = "#EF4444";
    public string Success { get; set; } = "#22C55E";
    public string BackgroundImage { get; set; } = "";

    public int CardCornerRadius { get; set; } = 16;
    public int ButtonCornerRadius { get; set; } = 12;
    public int InputCornerRadius { get; set; } = 10;
    public int SidebarWidth { get; set; } = 240;
    public double CardShadowDepth { get; set; } = 0;
    public double CardShadowBlur { get; set; } = 0;
    public double CardShadowOpacity { get; set; } = 0;
    public string CardShadowColor { get; set; } = "#000000";
    public double CardBorderThickness { get; set; } = 1;
    public int NavButtonHeight { get; set; } = 44;

    public static Dictionary<string, Theme> GetPresets(string uiStyle, string mode)
    {
        return uiStyle switch
        {
            "minecraft" => mode == "light" ? MinecraftLight : MinecraftDark,
            "frosted" => mode == "light" ? FrostedLight : FrostedDark,
            "flat" => mode == "light" ? FlatLight : FlatDark,
            "cyberpunk" => mode == "light" ? CyberpunkLight : CyberpunkDark,
            "wanderingearth" => mode == "light" ? WanderingEarthLight : WanderingEarthDark,
            _ => mode == "light" ? MinimalLight : MinimalDark,
        };
    }

    public static Theme ForMode(string presetName, string mode, string uiStyle = "minimal")
    {
        var presets = GetPresets(uiStyle, mode);
        return presets.TryGetValue(presetName, out var theme)
            ? theme
            : presets.Values.FirstOrDefault()
                ?? GetPresets("minimal", "dark").Values.First();
    }

    #region Minimal Dark
    public static Dictionary<string, Theme> MinimalDark { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#3CB043", Bg = "#2B2B3D", Card = "#363649", Sidebar = "#1E1E2E", Border = "#4A4A60", Text = "#E0E0E0", TextMuted = "#8A8AA0" },
        ["泥土"] = new() { Name = "泥土", Primary = "#8B6914", Bg = "#3D2E1A", Card = "#4A3820", Sidebar = "#2E2212", Border = "#5C4828", Text = "#F0E6D0", TextMuted = "#B0A080" },
        ["钻石"] = new() { Name = "钻石", Primary = "#4AEDD9", Bg = "#1A2A3A", Card = "#203444", Sidebar = "#142432", Border = "#2A4A5A", Text = "#D0F0F0", TextMuted = "#7AB0B0" },
        ["红石"] = new() { Name = "红石", Primary = "#E03030", Bg = "#2A1A1A", Card = "#382424", Sidebar = "#201212", Border = "#502020", Text = "#F0D0D0", TextMuted = "#B07070" },
    };
    #endregion

    #region Minimal Light (Spark NEO default)
    public static Dictionary<string, Theme> MinimalLight { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#2B86E5", Bg = "#E8EEF4", Card = "#FFFFFF", Sidebar = "#FFFFFF", Border = "#E2E8F0", Text = "#1E293B", TextMuted = "#94A3B8", CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1 },
        ["极光绿"] = new() { Name = "极光绿", Primary = "#059669", Bg = "#ECF5F1", Card = "#FFFFFF", Sidebar = "#FFFFFF", Border = "#D1E8DC", Text = "#064E3B", TextMuted = "#6B9A80", CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1 },
        ["樱花粉"] = new() { Name = "樱花粉", Primary = "#DB2777", Bg = "#F5ECF0", Card = "#FFFFFF", Sidebar = "#FFFFFF", Border = "#E8D0DC", Text = "#500A20", TextMuted = "#A06070", CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1 },
        ["深空紫"] = new() { Name = "深空紫", Primary = "#7C3AED", Bg = "#F0ECF8", Card = "#FFFFFF", Sidebar = "#FFFFFF", Border = "#D8D0E8", Text = "#2E1065", TextMuted = "#7060A0", CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1 },
    };
    #endregion

    #region Minecraft Dark
    public static Dictionary<string, Theme> MinecraftDark { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#3CB043", Bg = "#2B2B3D", Card = "#363649", Sidebar = "#1E1E2E", Border = "#4A4A60", Text = "#E0E0E0", TextMuted = "#8A8AA0", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["泥土"] = new() { Name = "泥土", Primary = "#8B6914", Bg = "#3D2E1A", Card = "#4A3820", Sidebar = "#2E2212", Border = "#5C4828", Text = "#F0E6D0", TextMuted = "#B0A080", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["钻石"] = new() { Name = "钻石", Primary = "#4AEDD9", Bg = "#1A2A3A", Card = "#203444", Sidebar = "#142432", Border = "#2A4A5A", Text = "#D0F0F0", TextMuted = "#7AB0B0", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["红石"] = new() { Name = "红石", Primary = "#E03030", Bg = "#2A1A1A", Card = "#382424", Sidebar = "#201212", Border = "#502020", Text = "#F0D0D0", TextMuted = "#B07070", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
    };
    #endregion

    #region Minecraft Light
    public static Dictionary<string, Theme> MinecraftLight { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#3CB043", Bg = "#C6C6C6", Card = "#D4D4D4", Sidebar = "#FFFFFF", Border = "#888888", Text = "#1A1A1A", TextMuted = "#555555", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["泥土"] = new() { Name = "泥土", Primary = "#8B6914", Bg = "#D4C8A0", Card = "#E0D4B0", Sidebar = "#FFFFFF", Border = "#998860", Text = "#2A2010", TextMuted = "#605030", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["钻石"] = new() { Name = "钻石", Primary = "#2AB0A0", Bg = "#B0D4D0", Card = "#C0E0DC", Sidebar = "#FFFFFF", Border = "#70A0A0", Text = "#0A2020", TextMuted = "#406060", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
        ["红石"] = new() { Name = "红石", Primary = "#C03030", Bg = "#D4B0B0", Card = "#E0C0C0", Sidebar = "#FFFFFF", Border = "#A06060", Text = "#200A0A", TextMuted = "#603030", CardCornerRadius = 0, ButtonCornerRadius = 0, InputCornerRadius = 0, CardBorderThickness = 2, NavButtonHeight = 40, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0 },
    };
    #endregion

    #region Frosted Dark
    public static Dictionary<string, Theme> FrostedDark { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#8B83F3", Bg = "#111C2E", Card = "#EAF1FF", Sidebar = "#19253A", Border = "#D6E3FA", Text = "#F5F8FF", TextMuted = "#AEBBD0", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.22, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["暮色"] = new() { Name = "暮色", Primary = "#FF9A7A", Bg = "#302024", Card = "#FFF0E9", Sidebar = "#3A272C", Border = "#F4D7D0", Text = "#FFF7F3", TextMuted = "#D9B8B2", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.22, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["星空"] = new() { Name = "星空", Primary = "#A399FF", Bg = "#171A38", Card = "#EEEAFE", Sidebar = "#23244A", Border = "#DCD7FF", Text = "#F8F6FF", TextMuted = "#B9B5D9", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.22, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["薄荷"] = new() { Name = "薄荷", Primary = "#38D5C8", Bg = "#0E302F", Card = "#E1FAF5", Sidebar = "#16413F", Border = "#C6F1E9", Text = "#F0FFFC", TextMuted = "#A6D8D0", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.22, CardBorderThickness = 1, NavButtonHeight = 44 },
    };
    #endregion

    #region Frosted Light
    public static Dictionary<string, Theme> FrostedLight { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#5E5CE6", Bg = "#BFD4EE", Card = "#FFFFFF", Sidebar = "#FFFFFF", Border = "#FFFFFF", Text = "#17253D", TextMuted = "#566A83", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.1, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["暮色"] = new() { Name = "暮色", Primary = "#D85C3B", Bg = "#E9D0C8", Card = "#FFFFFF", Sidebar = "#FFF9F7", Border = "#FFFFFF", Text = "#3A211B", TextMuted = "#805D53", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.1, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["星空"] = new() { Name = "星空", Primary = "#655CE0", Bg = "#D4D0ED", Card = "#FFFFFF", Sidebar = "#FBFAFF", Border = "#FFFFFF", Text = "#25213D", TextMuted = "#625D88", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.1, CardBorderThickness = 1, NavButtonHeight = 44 },
        ["薄荷"] = new() { Name = "薄荷", Primary = "#008F8B", Bg = "#C3E5DE", Card = "#FFFFFF", Sidebar = "#F8FFFC", Border = "#FFFFFF", Text = "#10342F", TextMuted = "#4E7A72", CardCornerRadius = 24, ButtonCornerRadius = 14, InputCornerRadius = 14, CardShadowDepth = 4, CardShadowBlur = 20, CardShadowOpacity = 0.1, CardBorderThickness = 1, NavButtonHeight = 44 },
    };
    #endregion

    #region Cyberpunk 2077 Dark
    public static Dictionary<string, Theme> CyberpunkDark { get; } = new()
    {
        ["夜之城"] = new() { Name = "夜之城", Primary = "#FCEE0A", Bg = "#080A0D", Card = "#15191F", Sidebar = "#0D1116", Border = "#3D4652", Text = "#F5F2E8", TextMuted = "#929BA6", Danger = "#FF3B62", Success = "#00E5FF", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["荒坂红"] = new() { Name = "荒坂红", Primary = "#FF2854", Bg = "#0D090D", Card = "#1A141A", Sidebar = "#120D13", Border = "#5A2938", Text = "#FFF2F4", TextMuted = "#C08F9C", Danger = "#FCEE0A", Success = "#00E5FF", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["赛博青"] = new() { Name = "赛博青", Primary = "#00E5FF", Bg = "#061115", Card = "#102329", Sidebar = "#091A1F", Border = "#285E69", Text = "#E8FEFF", TextMuted = "#86B8C1", Danger = "#FF4268", Success = "#FCEE0A", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["狗镇"] = new() { Name = "狗镇", Primary = "#FF7A18", Bg = "#100D0A", Card = "#211913", Sidebar = "#17110C", Border = "#65482D", Text = "#FFF3DF", TextMuted = "#B99B78", Danger = "#FF3B62", Success = "#FCEE0A", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
    };
    #endregion

    #region Cyberpunk 2077 Light
    public static Dictionary<string, Theme> CyberpunkLight { get; } = new()
    {
        ["夜之城"] = new() { Name = "夜之城", Primary = "#A39A00", Bg = "#E9ECE8", Card = "#F8FAF7", Sidebar = "#DCE2DD", Border = "#AAB5AD", Text = "#13191A", TextMuted = "#536265", Danger = "#C51F45", Success = "#007F91", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["荒坂红"] = new() { Name = "荒坂红", Primary = "#D51D48", Bg = "#F0E9EB", Card = "#FFF9FA", Sidebar = "#E4D8DC", Border = "#C5AEB6", Text = "#241419", TextMuted = "#755762", Danger = "#9B7800", Success = "#007F91", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["赛博青"] = new() { Name = "赛博青", Primary = "#007F91", Bg = "#E2EFF0", Card = "#F8FEFE", Sidebar = "#D2E3E5", Border = "#9EBFC4", Text = "#102326", TextMuted = "#4A6D72", Danger = "#C51F45", Success = "#A39A00", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["狗镇"] = new() { Name = "狗镇", Primary = "#B95508", Bg = "#F0E8DD", Card = "#FFFCF6", Sidebar = "#E5D7C4", Border = "#C7AB88", Text = "#2A1C12", TextMuted = "#765E43", Danger = "#C51F45", Success = "#A39A00", CardCornerRadius = 2, ButtonCornerRadius = 3, InputCornerRadius = 2, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
    };
    #endregion

    #region The Wandering Earth 550W Dark
    public static Dictionary<string, Theme> WanderingEarthDark { get; } = new()
    {
        ["550W 核心"] = new() { Name = "550W 核心", Primary = "#FF8A1F", Bg = "#090E14", Card = "#17212B", Sidebar = "#0D151D", Border = "#3B5669", Text = "#E7F4FA", TextMuted = "#86A1AF", Danger = "#F04444", Success = "#66D9FF", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["推进器阵列"] = new() { Name = "推进器阵列", Primary = "#FFB02E", Bg = "#101419", Card = "#202B31", Sidebar = "#141D23", Border = "#536873", Text = "#F2F4E9", TextMuted = "#9AA7A5", Danger = "#EE4D43", Success = "#8DE3FF", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["冰封轨道"] = new() { Name = "冰封轨道", Primary = "#78C9EA", Bg = "#07141D", Card = "#142832", Sidebar = "#0C1C25", Border = "#3F7485", Text = "#E2F8FF", TextMuted = "#85B4C2", Danger = "#F05A63", Success = "#FFB02E", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["地下城协议"] = new() { Name = "地下城协议", Primary = "#D8B36A", Bg = "#11100D", Card = "#27231B", Sidebar = "#191711", Border = "#6E6047", Text = "#FFF4D6", TextMuted = "#B2A27D", Danger = "#E84E42", Success = "#7DD5E8", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
    };
    #endregion

    #region The Wandering Earth 550W Light
    public static Dictionary<string, Theme> WanderingEarthLight { get; } = new()
    {
        ["550W 核心"] = new() { Name = "550W 核心", Primary = "#C85E00", Bg = "#E3E8E8", Card = "#F7FAF8", Sidebar = "#CFD9DA", Border = "#8EA5AA", Text = "#18262B", TextMuted = "#5C7279", Danger = "#B82E35", Success = "#147C9B", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["推进器阵列"] = new() { Name = "推进器阵列", Primary = "#B36A00", Bg = "#E7E8E3", Card = "#FBFCF6", Sidebar = "#D7D9D1", Border = "#A2A99C", Text = "#25291F", TextMuted = "#68705F", Danger = "#B5342E", Success = "#237A92", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["冰封轨道"] = new() { Name = "冰封轨道", Primary = "#237C9A", Bg = "#DDECEF", Card = "#F6FCFD", Sidebar = "#C8DDE2", Border = "#89AEB8", Text = "#172A31", TextMuted = "#58727B", Danger = "#B83445", Success = "#AD6800", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["地下城协议"] = new() { Name = "地下城协议", Primary = "#946D2D", Bg = "#E9E3D8", Card = "#FCF9F1", Sidebar = "#D8CFBF", Border = "#AA997D", Text = "#30291D", TextMuted = "#71634D", Danger = "#B33B32", Success = "#287D8E", CardCornerRadius = 1, ButtonCornerRadius = 2, InputCornerRadius = 1, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
    };
    #endregion

    #region Flat Dark
    public static Dictionary<string, Theme> FlatDark { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#0984E3", Bg = "#1A1A2E", Card = "#22223A", Sidebar = "#16162A", Border = "#2A2A40", Text = "#E0E0E0", TextMuted = "#8A8AA0", CardCornerRadius = 4, ButtonCornerRadius = 4, InputCornerRadius = 4, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, NavButtonHeight = 38 },
        ["珊瑚"] = new() { Name = "珊瑚", Primary = "#FF7675", Bg = "#1E1E2A", Card = "#282838", Sidebar = "#1A1A26", Border = "#3A3A50", Text = "#E8E0E0", TextMuted = "#A09090", CardCornerRadius = 4, ButtonCornerRadius = 4, InputCornerRadius = 4, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, NavButtonHeight = 38 },
        ["日出"] = new() { Name = "日出", Primary = "#FDCB6E", Bg = "#1E1A18", Card = "#282420", Sidebar = "#1A1614", Border = "#3A3428", Text = "#F0ECE0", TextMuted = "#A09880", CardCornerRadius = 4, ButtonCornerRadius = 4, InputCornerRadius = 4, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, NavButtonHeight = 38 },
        ["海洋"] = new() { Name = "海洋", Primary = "#00B894", Bg = "#121A1E", Card = "#1A2428", Sidebar = "#0E161A", Border = "#203038", Text = "#D0F0E8", TextMuted = "#70A090", CardCornerRadius = 4, ButtonCornerRadius = 4, InputCornerRadius = 4, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, NavButtonHeight = 38 },
    };
    #endregion

    #region Flat Light
    public static Dictionary<string, Theme> FlatLight { get; } = new()
    {
        ["默认"] = new() { Name = "默认", Primary = "#2563EB", Bg = "#F3F6FA", Card = "#FFFFFF", Sidebar = "#EAF1F9", Border = "#D6E0EB", Text = "#1E293B", TextMuted = "#64748B", CardCornerRadius = 8, ButtonCornerRadius = 6, InputCornerRadius = 6, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["珊瑚"] = new() { Name = "珊瑚", Primary = "#E25555", Bg = "#FAF5F5", Card = "#FFFFFF", Sidebar = "#F7EDED", Border = "#E6D5D5", Text = "#3A2525", TextMuted = "#806A6A", CardCornerRadius = 8, ButtonCornerRadius = 6, InputCornerRadius = 6, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["日出"] = new() { Name = "日出", Primary = "#D97706", Bg = "#FAF7F2", Card = "#FFFFFF", Sidebar = "#F4EEE5", Border = "#E5D9C8", Text = "#33281D", TextMuted = "#7D6B56", CardCornerRadius = 8, ButtonCornerRadius = 6, InputCornerRadius = 6, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
        ["海洋"] = new() { Name = "海洋", Primary = "#008F83", Bg = "#F1F8F7", Card = "#FFFFFF", Sidebar = "#E5F2F0", Border = "#C8DFDC", Text = "#143331", TextMuted = "#527570", CardCornerRadius = 8, ButtonCornerRadius = 6, InputCornerRadius = 6, CardShadowDepth = 0, CardShadowBlur = 0, CardShadowOpacity = 0, CardBorderThickness = 1, NavButtonHeight = 40 },
    };
    #endregion
}
