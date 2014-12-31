﻿using System;
using System.Linq;
using Color = System.Drawing.Color;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

using Orbwalk = MasterSeries.Common.M_Orbwalker;

namespace MasterSeries.Champions
{
    class Jax : Program
    {
        private int Sheen = 3057, Trinity = 3078;
        private bool WardCasted = false, ECasted = false;
        private int RCount = 0;
        private Vector3 WardPlacePos = default(Vector3);

        public Jax()
        {
            Q = new Spell(SpellSlot.Q, 700);
            W = new Spell(SpellSlot.W, 300);
            E = new Spell(SpellSlot.E, 375);
            R = new Spell(SpellSlot.R, 100);
            Q.SetTargetted(0.5f, float.MaxValue);
            W.SetTargetted(0.0435f, float.MaxValue);

            var ChampMenu = new Menu("Plugin", Name + "Plugin");
            {
                var ComboMenu = new Menu("Combo", "Combo");
                {
                    ItemBool(ComboMenu, "Q", "Use Q");
                    ItemBool(ComboMenu, "W", "Use W");
                    ItemBool(ComboMenu, "E", "Use E");
                    ItemBool(ComboMenu, "R", "Use R");
                    ItemList(ComboMenu, "RMode", "-> Mode", new[] { "Player Hp", "# Enemy" });
                    ItemSlider(ComboMenu, "RUnder", "--> If Hp Under", 40);
                    ItemSlider(ComboMenu, "RCount", "--> If Enemy Above", 2, 1, 4);
                    ItemBool(ComboMenu, "Item", "Use Item");
                    ItemBool(ComboMenu, "Ignite", "Auto Ignite If Killable");
                    ChampMenu.AddSubMenu(ComboMenu);
                }
                var HarassMenu = new Menu("Harass", "Harass");
                {
                    ItemBool(HarassMenu, "Q", "Use Q");
                    ItemSlider(HarassMenu, "QAbove", "-> If Hp Above", 20);
                    ItemBool(HarassMenu, "W", "Use W");
                    ItemBool(HarassMenu, "E", "Use E");
                    ChampMenu.AddSubMenu(HarassMenu);
                }
                var ClearMenu = new Menu("Lane/Jungle Clear", "Clear");
                {
                    var SmiteMob = new Menu("Smite Mob If Killable", "SmiteMob");
                    {
                        ItemBool(SmiteMob, "Baron", "Baron Nashor");
                        ItemBool(SmiteMob, "Dragon", "Dragon");
                        ItemBool(SmiteMob, "Red", "Red Brambleback");
                        ItemBool(SmiteMob, "Blue", "Blue Sentinel");
                        ItemBool(SmiteMob, "Krug", "Ancient Krug");
                        ItemBool(SmiteMob, "Gromp", "Gromp");
                        ItemBool(SmiteMob, "Raptor", "Crimson Raptor");
                        ItemBool(SmiteMob, "Wolf", "Greater Murk Wolf");
                        ClearMenu.AddSubMenu(SmiteMob);
                    }
                    ItemBool(ClearMenu, "Q", "Use Q");
                    ItemBool(ClearMenu, "W", "Use W");
                    ItemBool(ClearMenu, "E", "Use E");
                    ItemBool(ClearMenu, "Item", "Use Tiamat/Hydra");
                    ChampMenu.AddSubMenu(ClearMenu);
                }
                var MiscMenu = new Menu("Misc", "Misc");
                {
                    ItemBool(MiscMenu, "WJPink", "Ward Jump Use Pink Ward", false);
                    ItemBool(MiscMenu, "WLastHit", "Use W To Last Hit");
                    ItemBool(MiscMenu, "QKillSteal", "Use Q To Kill Steal");
                    ItemBool(MiscMenu, "EAntiGap", "Use E To Anti Gap Closer");
                    ItemBool(MiscMenu, "EInterrupt", "Use E To Interrupt");
                    ItemSlider(MiscMenu, "CustomSkin", "Skin Changer", 8, 0, 8).ValueChanged += SkinChanger;
                    ChampMenu.AddSubMenu(MiscMenu);
                }
                var DrawMenu = new Menu("Draw", "Draw");
                {
                    ItemBool(DrawMenu, "Q", "Q Range", false);
                    ItemBool(DrawMenu, "E", "E Range", false);
                    ChampMenu.AddSubMenu(DrawMenu);
                }
                Config.AddSubMenu(ChampMenu);
            }
            Game.OnGameUpdate += OnGameUpdate;
            Drawing.OnDraw += OnDraw;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnPossibleToInterrupt;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Orbwalk.AfterAttack += AfterAttack;
        }

        private void OnGameUpdate(EventArgs args)
        {
            if (Player.IsDead || MenuGUI.IsChatOpen || Player.IsChannelingImportantSpell() || Player.IsRecalling())
            {
                if (Player.IsDead) RCount = 0;
                return;
            }
            if (Orbwalk.CurrentMode == Orbwalk.Mode.Combo || Orbwalk.CurrentMode == Orbwalk.Mode.Harass)
            {
                NormalCombo(Orbwalk.CurrentMode.ToString());
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LaneClear)
            {
                LaneJungClear();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.LastHit)
            {
                LastHit();
            }
            else if (Orbwalk.CurrentMode == Orbwalk.Mode.Flee) WardJump(Game.CursorPos);
            if (ItemBool("Misc", "QKillSteal")) KillSteal();
        }

        private void OnDraw(EventArgs args)
        {
            if (Player.IsDead) return;
            if (ItemBool("Draw", "Q") && Q.Level > 0) Utility.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.Green : Color.Red);
            if (ItemBool("Draw", "E") && E.Level > 0) Utility.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.Green : Color.Red);
        }

        private void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (!ItemBool("Misc", "EAntiGap") || Player.IsDead || !E.CanCast(gapcloser.Sender)) return;
            E.Cast(PacketCast());
        }

        private void OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (!ItemBool("Misc", "EInterrupt") || Player.IsDead || !E.IsReady()) return;
            if (!E.InRange(unit) && Q.CanCast(unit) && Player.Mana >= Q.Instance.ManaCost + E.Instance.ManaCost) Q.CastOnUnit(unit, PacketCast());
            if (E.InRange(unit)) E.Cast(PacketCast());
        }

        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;
            if (args.SData.IsAutoAttack() && ((Obj_AI_Minion)args.Target).IsValidTarget(Orbwalk.GetAutoAttackRange(Player, (Obj_AI_Minion)args.Target) + 20) && ItemBool("Misc", "WLastHit") && W.IsReady() && Orbwalk.CurrentMode == Orbwalk.Mode.LastHit && CanKill((Obj_AI_Minion)args.Target, W, GetBonusDmg((Obj_AI_Minion)args.Target)) && args.Target is Obj_AI_Minion) W.Cast(PacketCast());
            if (args.SData.Name == "JaxCounterStrike")
            {
                ECasted = true;
                Utility.DelayAction.Add(1800, () => ECasted = false);
            }
            if (args.SData.Name == "jaxrelentlessattack") RCount = 0;
        }

        private void AfterAttack(AttackableUnit Target)
        {
            if (R.Level > 0) RCount += 1;
            if (W.IsReady() && Target.IsValidTarget(Orbwalk.GetAutoAttackRange(Player, Target) + 20) && Target is Obj_AI_Base)
            {
                switch (Orbwalk.CurrentMode)
                {
                    case Orbwalk.Mode.Combo:
                        if (ItemBool("Combo", "W")) W.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.Harass:
                        if (ItemBool("Harass", "W") && (!ItemBool("Harass", "Q") || (ItemBool("Harass", "Q") && !Q.IsReady()))) W.Cast(PacketCast());
                        break;
                    case Orbwalk.Mode.LaneClear:
                        if (ItemBool("Clear", "W")) W.Cast(PacketCast());
                        break;
                }
            }
            if (!W.IsReady() && !Player.HasBuff("JaxEmpowerTwo") && Orbwalk.CurrentMode == Orbwalk.Mode.Combo && ItemBool("Combo", "W") && ItemBool("Combo", "Item") && Target is Obj_AI_Hero) UseItem((Obj_AI_Hero)Target, true);
        }

        private void NormalCombo(string Mode)
        {
            if (targetObj == null) return;
            if (ItemBool(Mode, "E") && E.IsReady())
            {
                if (!Player.HasBuff("JaxEvasion"))
                {
                    if ((ItemBool(Mode, "Q") && Q.CanCast(targetObj)) || E.InRange(targetObj)) E.Cast(PacketCast());
                }
                else if (E.InRange(targetObj) && Player.Distance3D(targetObj) >= E.Range - 20) E.Cast(PacketCast());
            }
            if (ItemBool(Mode, "W") && W.IsReady() && ItemBool(Mode, "Q") && Q.CanCast(targetObj) && CanKill(targetObj, Q, 0, Q.GetDamage(targetObj) + GetBonusDmg(targetObj))) W.Cast(PacketCast());
            if (ItemBool(Mode, "Q") && Q.CanCast(targetObj) && (CanKill(targetObj, Q) || (Player.HasBuff("JaxEmpowerTwo") && CanKill(targetObj, Q, 0, Q.GetDamage(targetObj) + GetBonusDmg(targetObj))) || ((Mode == "Combo" || (Mode == "Harass" && Player.HealthPercentage() >= ItemList(Mode, "QAbove"))) && ((ItemBool(Mode, "E") && E.IsReady() && Player.HasBuff("JaxEvasion") && !E.InRange(targetObj)) || Player.Distance3D(targetObj) > 450)))) Q.CastOnUnit(targetObj, PacketCast());
            if (Mode == "Combo" && ItemBool(Mode, "R") && R.IsReady())
            {
                switch (ItemList(Mode, "RMode"))
                {
                    case 0:
                        if (Player.HealthPercentage() <= ItemSlider(Mode, "RUnder") && Q.InRange(targetObj)) R.Cast(PacketCast());
                        break;
                    case 1:
                        if (Player.CountEnemysInRange((int)Q.Range) >= ItemSlider(Mode, "RCount")) R.Cast(PacketCast());
                        break;
                }
            }
            if (Mode == "Combo" && ItemBool(Mode, "Item")) UseItem(targetObj);
            if (Mode == "Combo" && ItemBool(Mode, "Ignite") && IgniteReady()) CastIgnite(targetObj);
        }

        private void LaneJungClear()
        {
            var minionObj = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth);
            foreach (var Obj in minionObj)
            {
                if (SmiteReady() && Obj.Team == GameObjectTeam.Neutral)
                {
                    if ((ItemBool("SmiteMob", "Baron") && Obj.Name.StartsWith("SRU_Baron")) || (ItemBool("SmiteMob", "Dragon") && Obj.Name.StartsWith("SRU_Dragon")) || (!Obj.Name.Contains("Mini") && (
                        (ItemBool("SmiteMob", "Red") && Obj.Name.StartsWith("SRU_Red")) || (ItemBool("SmiteMob", "Blue") && Obj.Name.StartsWith("SRU_Blue")) ||
                        (ItemBool("SmiteMob", "Krug") && Obj.Name.StartsWith("SRU_Krug")) || (ItemBool("SmiteMob", "Gromp") && Obj.Name.StartsWith("SRU_Gromp")) ||
                        (ItemBool("SmiteMob", "Raptor") && Obj.Name.StartsWith("SRU_Razorbeak")) || (ItemBool("SmiteMob", "Wolf") && Obj.Name.StartsWith("SRU_Murkwolf"))))) CastSmite(Obj);
                }
                if (ItemBool("Clear", "E") && E.IsReady() && (Obj.MaxHealth >= 1200 || minionObj.Count(i => i.Distance3D(Obj) <= E.Range) >= 2))
                {
                    if (!Player.HasBuff("JaxEvasion"))
                    {
                        if ((ItemBool("Clear", "Q") && Q.IsReady()) || E.InRange(Obj)) E.Cast(PacketCast());
                    }
                    else if (E.InRange(Obj) && !ECasted) E.Cast(PacketCast());
                }
                if (ItemBool("Clear", "W") && W.IsReady() && ItemBool("Clear", "Q") && Q.IsReady() && CanKill(Obj, Q, 0, Q.GetDamage(Obj) + GetBonusDmg(Obj))) W.Cast(PacketCast());
                if (ItemBool("Clear", "Q") && Q.IsReady() && (CanKill(Obj, Q) || (Player.HasBuff("JaxEmpowerTwo") && CanKill(Obj, Q, 0, Q.GetDamage(Obj) + GetBonusDmg(Obj))) || (ItemBool("Clear", "E") && E.IsReady() && Player.HasBuff("JaxEvasion") && !E.InRange(Obj)) || Player.Distance3D(Obj) > 450)) Q.CastOnUnit(Obj, PacketCast());
                if (ItemBool("Clear", "Item")) UseItem(Obj, true);
            }
        }

        private void LastHit()
        {
            if (!ItemBool("Misc", "WLastHit") || !W.IsReady() || !Player.HasBuff("JaxEmpowerTwo")) return;
            foreach (var Obj in MinionManager.GetMinions(Orbwalk.GetAutoAttackRange() + 100, MinionTypes.All, MinionTeam.NotAlly).Where(i => CanKill(i, W, GetBonusDmg(i))).OrderByDescending(i => i.Distance3D(Player)))
            {
                Orbwalk.SetAttack(false);
                Player.IssueOrder(GameObjectOrder.AttackUnit, Obj);
                Orbwalk.SetAttack(true);
                break;
            }
        }

        private void WardJump(Vector3 Pos)
        {
            if (!Q.IsReady()) return;
            bool Casted = false;
            var JumpPos = Pos;
            if (GetWardSlot() != null && !WardCasted && Player.Position.Distance(JumpPos) > GetWardRange()) JumpPos = Player.Position.Extend(JumpPos, GetWardRange());
            foreach (var Obj in ObjectManager.Get<Obj_AI_Base>().Where(i => i.IsValidTarget(Q.Range + i.BoundingRadius, false, Player.Position) && !i.IsMe && !(i is Obj_AI_Turret) && i.Position.Distance(WardCasted ? WardPlacePos : JumpPos) < 200).OrderBy(i => i.Position.Distance(WardCasted ? WardPlacePos : JumpPos)))
            {
                Q.CastOnUnit(Obj, PacketCast());
                Casted = true;
                return;
            }
            if (!Casted && GetWardSlot() != null && !WardCasted)
            {
                Player.Spellbook.CastSpell(GetWardSlot().SpellSlot, JumpPos);
                WardPlacePos = JumpPos;
                Utility.DelayAction.Add(800, () => WardPlacePos = default(Vector3));
                WardCasted = true;
                Utility.DelayAction.Add(800, () => WardCasted = false);
            }
        }

        private void KillSteal()
        {
            if (!Q.IsReady()) return;
            foreach (var Obj in ObjectManager.Get<Obj_AI_Hero>().Where(i => i.IsValidTarget(Q.Range) && CanKill(i, Q, 0, Q.GetDamage(i) + (((W.IsReady() || Player.HasBuff("JaxEmpowerTwo")) && !CanKill(i, Q)) ? GetBonusDmg(i) : 0)) && i != targetObj).OrderBy(i => i.Health).OrderBy(i => i.Distance3D(Player)))
            {
                if (W.IsReady() && !CanKill(Obj, Q)) W.Cast(PacketCast());
                if (Q.IsReady() && ((!CanKill(Obj, Q) && Player.HasBuff("JaxEmpowerTwo")) || CanKill(Obj, Q))) Q.CastOnUnit(Obj, PacketCast());
            }
        }

        private void UseItem(Obj_AI_Base Target, bool IsFarm = false)
        {
            if (Items.CanUseItem(Bilgewater) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(Bilgewater, Target);
            if (Items.CanUseItem(HexGun) && Player.Distance3D(Target) <= 700 && !IsFarm) Items.UseItem(HexGun, Target);
            if (Items.CanUseItem(BladeRuined) && Player.Distance3D(Target) <= 450 && !IsFarm) Items.UseItem(BladeRuined, Target);
            if (Items.CanUseItem(Tiamat) && IsFarm ? Player.Distance3D(Target) <= 350 : Player.CountEnemysInRange(350) >= 1) Items.UseItem(Tiamat);
            if (Items.CanUseItem(Hydra) && IsFarm ? Player.Distance3D(Target) <= 350 : (Player.CountEnemysInRange(350) >= 2 || (Player.GetAutoAttackDamage(Target, true) < Target.Health && Player.CountEnemysInRange(350) == 1))) Items.UseItem(Hydra);
            if (Items.CanUseItem(Randuin) && Player.CountEnemysInRange(450) >= 1 && !IsFarm) Items.UseItem(Randuin);
        }

        private double GetBonusDmg(Obj_AI_Base Target)
        {
            double DmgItem = 0;
            if (Items.HasItem(Sheen) && ((Items.CanUseItem(Sheen) && W.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage > DmgItem) DmgItem = Player.BaseAttackDamage;
            if (Items.HasItem(Trinity) && ((Items.CanUseItem(Trinity) && W.IsReady()) || Player.HasBuff("Sheen")) && Player.BaseAttackDamage * 2 > DmgItem) DmgItem = Player.BaseAttackDamage * 2;
            return ((W.IsReady() || Player.HasBuff("JaxEmpowerTwo")) ? W.GetDamage(Target) : 0) + (RCount >= 2 ? R.GetDamage(Target) : 0) + Player.GetAutoAttackDamage(Target, true) + Player.CalcDamage(Target, Damage.DamageType.Physical, DmgItem);
        }
    }
}