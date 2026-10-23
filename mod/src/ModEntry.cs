using System;
using DTMAPI.Abstractions;
using HarmonyLib;

namespace Yinch.CelesteMovement;

public sealed class ModConfig
{
    public string GrabKey { get; set; } = "LeftControl";
    public string CrouchDashKey { get; set; } = "C";
    public string LeftKey { get; set; } = "A, LeftArrow";
    public string RightKey { get; set; } = "D, RightArrow";
    public string UpKey { get; set; } = "W, UpArrow";
    public string DownKey { get; set; } = "S, DownArrow";
    public bool UseGameJumpDash { get; set; } = true;
    public string JumpKey { get; set; } = "Space";
    public string DashKey { get; set; } = "LeftShift";
    public bool DiagnosticLogging { get; set; } = false;
    public double SpatialScale { get; set; } = .5;
}

public sealed class ModEntry : DtmMod, IDisposable
{
    private IDtmHelper helper;
    private Harmony harmony;
    private IInputRegistration grab, crouchDash, jump, dash;
    private IInputRegistration left, right, up, down;
    private IDisposable diagnosticCommand;
    private ModConfig config;
    private bool disposed;

    public override void Entry(IDtmHelper helper)
    {
        this.helper = helper;
        config = helper.ReadConfig<ModConfig>();
        Normalize();
        MovementRuntime.Initialize(config, helper.Monitor, helper.Input, helper.GetOptionalService<IDtmRuntimeContext>());
        harmony = new Harmony("Yinch.CelesteMovement.Native");
        try
        {
            harmony.PatchAll(typeof(ModEntry).Assembly);
            RegisterBindings();
            var commands=helper.GetOptionalService<IDtmCommands>();
            if(commands!=null) diagnosticCommand=commands.Register("probe", context =>
            {
                var state=MovementRuntime.Current(DolocAPI.agent);
                if(state==null) { context.WriteLine("Movement is not active."); return; }
                string mode=context.Arguments.Count>0?context.Arguments[0]:"status";
                if(mode!="status")
                {
                    if(!config.DiagnosticLogging) { context.WriteLine("Enable diagnostic logging before scripted probes."); return; }
                    state.StartProbe(mode);
                }
                context.WriteLine(state.Describe());
            }, description:"Diagnostic status or scripted movement probe. Transition walks toward room exits for up to 600 ticks; other probes run 90 ticks. Test save only.", parameters:"status|jump|dash|super|hyper|extended|transition");
            helper.Events.GameLoop.ReturnedToTitle += OnTitle;
            RegisterConfig();
            helper.Monitor.Log($"Celeste Movement 0.2.10 loaded; always active during locomotion. {config.GrabKey} grab; {config.CrouchDashKey} crouch-dash; jump/dash={(config.UseGameJumpDash?"game":config.JumpKey+"/"+config.DashKey)}.");
        }
        catch { Dispose(); throw; }
    }

    private void OnTitle(object sender, ReturnedToTitleEventArgs e) => MovementRuntime.Forget();
    private void RegisterBindings()
    {
        left?.Dispose(); right?.Dispose(); up?.Dispose(); down?.Dispose();
        grab?.Dispose(); crouchDash?.Dispose(); jump?.Dispose(); dash?.Dispose();
        left=helper.Input.RegisterKeybind("MoveLeft",config.LeftKey);
        right=helper.Input.RegisterKeybind("MoveRight",config.RightKey);
        up=helper.Input.RegisterKeybind("MoveUp",config.UpKey);
        down=helper.Input.RegisterKeybind("MoveDown",config.DownKey);
        grab=helper.Input.RegisterKeybind("Grab",config.GrabKey);
        crouchDash=helper.Input.RegisterKeybind("CrouchDash",config.CrouchDashKey);
        // Unused custom keys must not appear as active gameplay registrations.
        jump=helper.Input.RegisterKeybind("Jump",config.UseGameJumpDash?"None":config.JumpKey);
        dash=helper.Input.RegisterKeybind("Dash",config.UseGameJumpDash?"None":config.DashKey);
    }

    private void Normalize()
    {
        if(double.IsNaN(config.SpatialScale)||double.IsInfinity(config.SpatialScale)) config.SpatialScale=.5;
        config.SpatialScale=Math.Max(.25,Math.Min(1,config.SpatialScale));
        if (string.IsNullOrWhiteSpace(config.GrabKey)) config.GrabKey = "LeftControl";
        if (string.IsNullOrWhiteSpace(config.CrouchDashKey)) config.CrouchDashKey = "C";
        if (string.IsNullOrWhiteSpace(config.LeftKey)) config.LeftKey="A, LeftArrow";
        if (string.IsNullOrWhiteSpace(config.RightKey)) config.RightKey="D, RightArrow";
        if (string.IsNullOrWhiteSpace(config.UpKey)) config.UpKey="W, UpArrow";
        if (string.IsNullOrWhiteSpace(config.DownKey)) config.DownKey="S, DownArrow";
        if (string.IsNullOrWhiteSpace(config.JumpKey)) config.JumpKey="Space";
        if (string.IsNullOrWhiteSpace(config.DashKey)) config.DashKey="LeftShift";
    }

    private void RegisterConfig()
    {
        var menu = helper.ModRegistry.GetApi<IDtmConfigMenuApi>("DTMAPI.ModConfigMenu");
        if (menu == null) { helper.Monitor.Log("Config menu unavailable; JSON configuration remains available.", LogLevel.Warn); return; }
        menu.Register(Manifest, () => { config.GrabKey="LeftControl"; config.CrouchDashKey="C"; config.LeftKey="A, LeftArrow"; config.RightKey="D, RightArrow"; config.UpKey="W, UpArrow"; config.DownKey="S, DownArrow"; config.UseGameJumpDash=true; config.JumpKey="Space"; config.DashKey="LeftShift"; config.SpatialScale=.5; config.DiagnosticLogging = false; }, () =>
        {
            Normalize(); MovementRuntime.Forget();
            RegisterBindings();
            helper.WriteConfig(config);
        });
        menu.SetDisplayName(Manifest, () => "蔚蓝运动 / Celeste Movement");
        menu.AddParagraph(Manifest, () => "0.2.10：固定蔚蓝运动。按下蹲伏，单向平台上按下＋跳下穿。修改后保存生效；方向键采用后按优先。");
        menu.AddParagraph(Manifest, () => "能力随存档进度：原版二段跳解锁一次冲刺，原版冲刺解锁两次冲刺。落地补满；不保留空中二段跳。满次数原发色；双冲剩一次红发，耗尽蓝发，未解锁保持原发色。");
        menu.AddNumberOption(Manifest, () => "空间比例", () => "默认 0.50，为 0.2.1 位移尺度的一半。同步改变速度、跳高、冲刺距离和碰撞尺寸；不改变动作时序。保存后生效。", () => config.SpatialScale, value => config.SpatialScale=value, .25,1,.05);
        menu.AddSectionTitle(Manifest, () => "键盘操作");
        Key(menu,"向左","方向键按后按优先规则处理。",()=>config.LeftKey,v=>config.LeftKey=v,"A, LeftArrow");
        Key(menu,"向右","方向键按后按优先规则处理。",()=>config.RightKey,v=>config.RightKey=v,"D, RightArrow");
        Key(menu,"向上","攀爬与冲刺方向。",()=>config.UpKey,v=>config.UpKey=v,"W, UpArrow");
        Key(menu,"向下 / 蹲伏","蹲伏、快速下落与冲刺方向；配合跳跃下穿平台。",()=>config.DownKey,v=>config.DownKey=v,"S, DownArrow");
        Key(menu,"抓墙","按住抓墙；上/下攀爬。",()=>config.GrabKey,v=>config.GrabKey=v,"LeftControl");
        Key(menu,"蹲姿冲刺","使用较矮的碰撞箱冲刺。无方向输入时沿面朝方向冲刺。",()=>config.CrouchDashKey,v=>config.CrouchDashKey=v,"C");
        menu.AddBoolOption(Manifest,()=>"跳跃 / 冲刺沿用游戏键位",()=>"默认开启，保持原来的操作。关闭后使用下方独立键位，仅在蔚蓝运动接管时生效。",()=>config.UseGameJumpDash,v=>config.UseGameJumpDash=v);
        Key(menu,"独立跳跃","关闭“沿用游戏键位”后生效；支持按住长跳、松开短跳。",()=>config.JumpKey,v=>config.JumpKey=v,"Space");
        Key(menu,"独立普通冲刺","关闭“沿用游戏键位”后生效；配合方向键进行八方向冲刺。",()=>config.DashKey,v=>config.DashKey=v,"LeftShift");
        menu.AddParagraph(Manifest,()=> "键位提示：同一动作支持多个候选键；每行可恢复默认。冲突提示只供参考，不会替你重绑其他 Mod 或游戏的交互/工具键。");
        menu.AddParagraph(Manifest,()=> {
            var conflicts=menu.GetKeybindConflicts(Manifest.UniqueID);
            return conflicts.Count==0?"配置菜单未发现键位重叠。":"可能的键位重叠："+string.Join("；",conflicts);
        });
        menu.AddBoolOption(Manifest, () => "诊断日志", () => "每秒记录一次位置和运动状态。", () => config.DiagnosticLogging, value => config.DiagnosticLogging = value);
        menu.AddButton(Manifest,()=>"播放动作预览",()=>"进入或返回游戏后播放 14 秒蹲伏、攀爬、冲刺变色和低体力红闪展示。只改变显示，不移动角色。",MovementRuntime.PreviewAnimations);
    }

    private void Key(IDtmConfigMenuApi menu,string name,string tip,Func<string> get,Action<string> set,string defaultValue)
    {
        var defaults=menu as IDtmConfigMenuKeybindDefaultsApi;
        if(defaults!=null) defaults.AddKeybindOption(Manifest,()=>name,()=>tip,get,set,()=>defaultValue);
        else menu.AddKeybindOption(Manifest,()=>name,()=>tip,get,set);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        MovementRuntime.Release();
        if (helper != null)
        {
            // Runtime can deactivate the owner before invoking Dispose; it then
            // owns event cleanup. Still release all local render/input resources.
            try { helper.Events.GameLoop.ReturnedToTitle -= OnTitle; }
            catch(InvalidOperationException error) when(error.Message.IndexOf("inactive",StringComparison.OrdinalIgnoreCase)>=0) { }
        }
        diagnosticCommand?.Dispose(); grab?.Dispose(); crouchDash?.Dispose(); harmony?.UnpatchSelf();
        left?.Dispose(); right?.Dispose(); up?.Dispose(); down?.Dispose();
        jump?.Dispose(); dash?.Dispose();
        MovementRuntime.Shutdown();
    }
}
