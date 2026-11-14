namespace Vela;

/// <summary>
/// A compact built-in icon set on a 24x24 grid, drawn with round strokes where
/// appropriate. Paths are adapted from the Lucide icon set (ISC license).
/// </summary>
public static class Icons
{
    public static IconData Plus { get; } = S("plus", "M12 5v14M5 12h14");
    public static IconData Minus { get; } = S("minus", "M5 12h14");
    public static IconData Check { get; } = S("check", "M20 6 9 17l-5-5");
    public static IconData X { get; } = S("x", "M18 6 6 18M6 6l12 12");
    public static IconData ChevronDown { get; } = S("chevron-down", "m6 9 6 6 6-6");
    public static IconData ChevronUp { get; } = S("chevron-up", "m18 15-6-6-6 6");
    public static IconData ChevronLeft { get; } = S("chevron-left", "m15 18-6-6 6-6");
    public static IconData ChevronRight { get; } = S("chevron-right", "m9 18 6-6-6-6");
    public static IconData ArrowRight { get; } = S("arrow-right", "M5 12h14m-6-6 6 6-6 6");
    public static IconData ArrowLeft { get; } = S("arrow-left", "M19 12H5m6-6-6 6 6 6");
    public static IconData ArrowUpRight { get; } = S("arrow-up-right", "M7 17 17 7M7 7h10v10");
    public static IconData Search { get; } = S("search", "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16M21 21l-4.35-4.35");
    public static IconData Home { get; } = S("home", "M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z");
    public static IconData Settings { get; } = S("settings", "M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6");
    public static IconData Folder { get; } = S("folder", "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z");
    public static IconData File { get; } = S("file", "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8zM14 2v6h6");
    public static IconData LayoutGrid { get; } = S("layout-grid", "M3 3h7v7H3zM14 3h7v7h-7zM14 14h7v7h-7zM3 14h7v7H3z");
    public static IconData List { get; } = S("list", "M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01");
    public static IconData BarChart { get; } = S("bar-chart", "M3 3v18h18M18 17V9M13 17V5M8 17v-3");
    public static IconData PieChart { get; } = S("pie-chart", "M21.21 15.89A10 10 0 1 1 8 2.83M22 12A10 10 0 0 0 12 2v10z");
    public static IconData Users { get; } = S("users", "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75");
    public static IconData User { get; } = S("user", "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2M12 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8");
    public static IconData Bell { get; } = S("bell", "M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9M13.73 21a2 2 0 0 1-3.46 0");
    public static IconData Sun { get; } = S("sun", "M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10M12 1v2M12 21v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M1 12h2M21 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42");
    public static IconData Moon { get; } = S("moon", "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z");
    public static IconData MoreHorizontal { get; } = S("more-horizontal", "M5 12h.01M12 12h.01M19 12h.01");
    public static IconData MoreVertical { get; } = S("more-vertical", "M12 5h.01M12 12h.01M12 19h.01");
    public static IconData Play { get; } = F("play", "m7 4 13 8-13 8z");
    public static IconData Pause { get; } = F("pause", "M6 4h4v16H6zM14 4h4v16h-4z");
    public static IconData Command { get; } = S("command", "M18 3a3 3 0 0 0-3 3v12a3 3 0 0 0 3 3 3 3 0 0 0 3-3 3 3 0 0 0-3-3H6a3 3 0 0 0-3 3 3 3 0 0 0 3 3 3 3 0 0 0 3-3V6a3 3 0 0 0-3-3 3 3 0 0 0-3 3 3 3 0 0 0 3 3h12a3 3 0 0 0 3-3 3 3 0 0 0-3-3z");
    public static IconData Sparkles { get; } = S("sparkles", "M12 3l1.9 5.8L20 11l-6.1 2.2L12 19l-1.9-5.8L4 11l6.1-2.2zM19 3v4M17 5h4");
    public static IconData Zap { get; } = S("zap", "M13 2 3 14h8l-1 8 10-12h-8z");
    public static IconData Code { get; } = S("code", "m16 18 6-6-6-6M8 6l-6 6 6 6");
    public static IconData Terminal { get; } = S("terminal", "m4 17 6-6-6-6M12 19h8");
    public static IconData Layers { get; } = S("layers", "m12.83 2.18a2 2 0 0 0-1.66 0L2.6 6.08a1 1 0 0 0 0 1.83l8.58 3.91a2 2 0 0 0 1.66 0l8.58-3.9a1 1 0 0 0 0-1.83zM2 12l8.58 3.91a2 2 0 0 0 1.66 0L22 12M2 17l8.58 3.91a2 2 0 0 0 1.66 0L22 17");
    public static IconData Clock { get; } = S("clock", "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20M12 6v6l4 2");
    public static IconData Calendar { get; } = S("calendar", "M8 2v4M16 2v4M3 10h18M5 4h14a2 2 0 0 1 2 2v13a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z");
    public static IconData Download { get; } = S("download", "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3");
    public static IconData Upload { get; } = S("upload", "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M17 8l-5-5-5 5M12 3v12");
    public static IconData Trash { get; } = S("trash", "M3 6h18M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6M10 11v6M14 11v6");
    public static IconData Edit { get; } = S("edit", "M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4z");
    public static IconData Copy { get; } = S("copy", "M8 8h12v12H8zM16 8V4H4v12h4");
    public static IconData ExternalLink { get; } = S("external-link", "M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6M15 3h6v6M10 14 21 3");
    public static IconData Info { get; } = S("info", "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20M12 16v-4M12 8h.01");
    public static IconData AlertTriangle { get; } = S("alert-triangle", "m21.73 18-8-14a2 2 0 0 0-3.46 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3zM12 9v4M12 17h.01");
    public static IconData CheckCircle { get; } = S("check-circle", "M22 11.08V12a10 10 0 1 1-5.93-9.14M22 4 12 14.01l-3-3");
    public static IconData HelpCircle { get; } = S("help-circle", "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20M9.09 9a3 3 0 1 1 5.83 1c0 2-3 3-3 3M12 17h.01");
    public static IconData Globe { get; } = S("globe", "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z");
    public static IconData Lock { get; } = S("lock", "M5 11h14v10H5zM8 11V7a4 4 0 0 1 8 0v4");
    public static IconData Mail { get; } = S("mail", "M4 4h16v16H4zM22 6l-10 7L2 6");
    public static IconData Message { get; } = S("message", "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z");
    public static IconData Filter { get; } = S("filter", "M22 3H2l8 9.46V19l4 2v-8.54z");
    public static IconData Star { get; } = S("star", "m12 2 3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01z");
    public static IconData Heart { get; } = S("heart", "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78L12 21.23l8.84-8.84a5.5 5.5 0 0 0 0-7.78z");
    public static IconData Palette { get; } = S("palette", "M12 22a1 1 0 0 1 0-20 10 10 0 0 1 10 10 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8 1.75 1.75 0 0 1-1.35 2.2zM7.5 10.5h.01M12 7.5h.01M16.5 10.5h.01");
    public static IconData PanelLeft { get; } = S("panel-left", "M3 3h18v18H3zM9 3v18");
    public static IconData Menu { get; } = S("menu", "M4 6h16M4 12h16M4 18h16");
    public static IconData Refresh { get; } = S("refresh", "M3 12a9 9 0 0 1 15-6.7L21 8M21 3v5h-5M21 12a9 9 0 0 1-15 6.7L3 16M3 21v-5h5");
    public static IconData Eye { get; } = S("eye", "M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6");
    public static IconData Archive { get; } = S("archive", "M3 4h18v4H3zM5 8v12h14V8M10 12h4");
    public static IconData Inbox { get; } = S("inbox", "M22 12h-6l-2 3h-4l-2-3H2M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z");
    public static IconData CreditCard { get; } = S("credit-card", "M3 5h18v14H3zM3 10h18");
    public static IconData Rocket { get; } = S("rocket", "M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09zM12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2zM9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5");
    public static IconData Database { get; } = S("database", "M12 8c4.97 0 9-1.34 9-3s-4.03-3-9-3-9 1.34-9 3 4.03 3 9 3zM3 5v14c0 1.66 4.03 3 9 3s9-1.34 9-3V5M3 12c0 1.66 4.03 3 9 3s9-1.34 9-3");
    public static IconData Cpu { get; } = S("cpu", "M9 9h6v6H9zM4 4h16v16H4zM9 1v3M15 1v3M9 20v3M15 20v3M1 9h3M1 15h3M20 9h3M20 15h3");
    public static IconData Cloud { get; } = S("cloud", "M18 10h-1.26A8 8 0 1 0 9 20h9a5 5 0 0 0 0-10z");
    public static IconData Wifi { get; } = S("wifi", "M5 12.55a11 11 0 0 1 14.08 0M1.42 9a16 16 0 0 1 21.16 0M8.53 16.11a6 6 0 0 1 6.95 0M12 20h.01");
    public static IconData Bookmark { get; } = S("bookmark", "M19 21 12 16l-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z");
    public static IconData ShoppingBag { get; } = S("shopping-bag", "M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4zM3 6h18M16 10a4 4 0 0 1-8 0");
    public static IconData Compass { get; } = S("compass", "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20M16.24 7.76l-2.12 6.36-6.36 2.12 2.12-6.36z");
    public static IconData Paperclip { get; } = S("paperclip", "m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48");
    public static IconData Send { get; } = S("send", "m22 2-7 20-4-9-9-4zM22 2 11 13");
    public static IconData Link { get; } = S("link", "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71");

    public static IReadOnlyList<IconData> All { get; } =
    [
        Plus, Minus, Check, X, ChevronDown, ChevronUp, ChevronLeft, ChevronRight,
        ArrowRight, ArrowLeft, ArrowUpRight, Search, Home, Settings, Folder, File,
        LayoutGrid, List, BarChart, PieChart, Users, User, Bell, Sun, Moon,
        MoreHorizontal, MoreVertical, Play, Pause, Command, Sparkles, Zap, Code,
        Terminal, Layers, Clock, Calendar, Download, Upload, Trash, Edit, Copy,
        ExternalLink, Info, AlertTriangle, CheckCircle, HelpCircle, Globe, Lock,
        Mail, Message, Filter, Star, Heart, Palette, PanelLeft, Menu, Refresh, Eye,
        Archive, Inbox, CreditCard, Rocket, Database, Cpu, Cloud, Wifi, Bookmark,
        ShoppingBag, Compass, Paperclip, Send, Link,
    ];

    private static IconData S(string name, string path) => IconData.FromSvg(name, path);
    private static IconData F(string name, string path) => IconData.FromSvg(name, path, filled: true);
}
