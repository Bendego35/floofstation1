using Content.Shared.Examine;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Humanoid;
using Content.Shared.Psionics;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Shadowkin;
using Content.Shared.Rejuvenate;
using Content.Shared.Alert;
using Content.Shared.Rounding;
using Content.Shared.Actions;
using Content.Server.Abilities.Psionics;
using Content.Server.Bed.Sleep;

namespace Content.Server.Shadowkin;
public sealed class ShadowkinSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilitiesSystem = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    public const string ShadowkinSleepActionId = "ShadowkinActionSleep";
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowkinComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ShadowkinComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ShadowkinComponent, OnMindbreakEvent>(OnMindbreak);
        SubscribeLocalEvent<ShadowkinComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnInit(EntityUid uid, ShadowkinComponent component, MapInitEvent args)
    {
        if (component.BlackeyeSpawn
        && TryComp<PsionicComponent>(uid, out var magic))
        {
            magic.Removable = true;
            _psionicAbilitiesSystem.MindBreak(uid);
        }

        _actionsSystem.AddAction(uid, ref component.ShadowkinSleepAction, ShadowkinSleepActionId, uid);

        UpdateShadowkinAlert(uid, component);
    }

    private void OnExamined(EntityUid uid, ShadowkinComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange
        || !TryComp<PsionicComponent>(uid, out var magic)
        || HasComp<MindbrokenComponent>(uid))
            return;

        // TODO: Set Mana Power Values to severity and apply severity.
        // var severity = "shadowkin-power-" + ContentHelpers.RoundToLevels(MathF.Max(0f, magic.power), magic.maxpower, 5);
        // var powerType = Loc.GetString(severity)
        var powerType = Loc.GetString("shadowkin-power-3");

        if (args.Examined == args.Examiner)
        {
            // TODO power, powerMax need to be the value of Psionic Mana
            args.PushMarkup(Loc.GetString("shadowkin-power-examined-self",
                ("power", "0"),
                ("powerMax", "100"),
                ("powerType", powerType)
            ));
        }
        else
        {
            // TODO power, powerMax need to be the value of Psionic Mana
            args.PushMarkup(Loc.GetString("shadowkin-power-examined-other",
                ("target", uid),
                ("powerType", powerType)
            ));
        }
    }

    /// <summary>
    /// Update the Shadowkin Alert, if Blackeye will remove the Alert, if not will update to its current power status.
    /// </summary>
    public void UpdateShadowkinAlert(EntityUid uid, ShadowkinComponent component)
    {
        if (TryComp<PsionicComponent>(uid, out var magic))
        {
            // TODO: Set Mana Power Values to severity and apply severity to alert.
            // _alerts.ShowAlert(uid, AlertType.ShadowkinPower, ContentHelpers.RoundToLevels(MathF.Max(0f, magic.power), magic.maxpower, 8));
            _alerts.ShowAlert(uid, AlertType.ShadowkinPower, 0);
        }
        else
        {
            _alerts.ClearAlert(uid, AlertType.ShadowkinPower);
        }
    }

    private void OnMindbreak(EntityUid uid, ShadowkinComponent component, ref OnMindbreakEvent args)
    {
        if (TryComp<MindbrokenComponent>(uid, out var mindbreak))
            mindbreak.MindbrokenExaminationText = "examine-mindbroken-shadowkin-message";

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            component.OldEyeColor = humanoid.EyeColor;
            humanoid.EyeColor = component.BlackEyeColor;
            Dirty(humanoid);
        }

        if (component.BlackeyeSpawn)
            return;

        if (TryComp<StaminaComponent>(uid, out var stamina))
            _stamina.TakeStaminaDamage(uid, stamina.CritThreshold, stamina, uid);

        if (!TryComp<DamageableComponent>(uid, out var damageable))
        {
            DamageSpecifier damage = new();
            damage.DamageDict.Add("Cellular", FixedPoint2.New(5));
            _damageable.TryChangeDamage(uid, damage);
        }
    }

    private void OnRejuvenate(EntityUid uid, ShadowkinComponent component, RejuvenateEvent args)
    {
        if (component.BlackeyeSpawn
        || !HasComp<MindbrokenComponent>(uid))
            return;

        RemComp<MindbrokenComponent>(uid);

        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            humanoid.EyeColor = component.OldEyeColor;
            Dirty(humanoid);
        }

        EnsureComp<PsionicComponent>(uid, out var magic);

        magic.Removable = false;
        magic.MindbreakingFeedback = "shadowkin-blackeye";
    }
}