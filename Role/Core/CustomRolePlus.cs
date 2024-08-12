﻿using Exiled.API.Features;
using Exiled.API.Features.DamageHandlers;
using Exiled.API.Features.Spawn;
using Exiled.CustomRoles.API;
using Exiled.CustomRoles.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Server;
using Exiled.Events.Features;
using Exiled.Loader;
using PlayerRoles;
using System.Collections.Generic;
using System.Linq;
using YongAnFrame.Core;
using YongAnFrame.Core.Data;
using YongAnFrame.Role.Core.Enums;
using YongAnFrame.Role.Core.Interfaces;

namespace YongAnFrame.Role.Core
{
    public abstract class CustomRolePlus : CustomRole
    {
        public override bool IgnoreSpawnSystem { get; set; } = false;
        public virtual SpawnAttributes SpawnAttributes { get; set; } = new SpawnAttributes();
        public bool IStaetSpawn { get; set; } = true;
        public Dictionary<FramePlayer, CustomRolePlusData> BaseData { get; } = [];
        public virtual MoreAttributes MoreAttributes { get; set; } = new MoreAttributes();
        public abstract string NameColor { get; set; }
        public Dictionary<uint, string> DeathText { get; } = [];
        public virtual RoleTypeId OldRole { get; set; } = RoleTypeId.None;

        #region Static
        public static List<FramePlayer> NoCustomRole { get; private set; } = [];
        public static List<FramePlayer> RespawnTeamPlayer { get; private set; } = [];
        public static Queue<CustomRole> RespawnCustomRole { get; private set; } = [];
        public static int SpawnChanceNum { get; private set; } = Loader.Random.StrictNext(1, 101);
        public static int RespawnWave { get; private set; } = 0;
        public static void SubscribeStaticEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted += new CustomEventHandler(OnStaticRoundStarted);
            Exiled.Events.Handlers.Server.RespawningTeam += new CustomEventHandler<RespawningTeamEventArgs>(OnStaticRespawningTeam);
            Exiled.Events.Handlers.Server.RestartingRound -= new CustomEventHandler(OnStaticRestartingRound);
        }
        public static void UnsubscribeStaticEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= new CustomEventHandler(OnStaticRoundStarted);
            Exiled.Events.Handlers.Server.RespawningTeam -= new CustomEventHandler<RespawningTeamEventArgs>(OnStaticRespawningTeam);
            Exiled.Events.Handlers.Server.RestartingRound -= new CustomEventHandler(OnStaticRestartingRound);
        }

        private static void OnStaticRestartingRound()
        {
            SpawnChanceNum = Loader.Random.StrictNext(1, 101);
        }

        private static void OnStaticRoundStarted()
        {
            foreach (var item in Player.List)
            {
                Log.Info(item);
            }

            Log.Info(Player.List.Select(FramePlayer.Get));
            NoCustomRole = Player.List.Select(FramePlayer.Get).ToList();
            RespawnWave = 0;
        }
        private static void OnStaticRespawningTeam(RespawningTeamEventArgs args)
        {
            RespawnTeamPlayer = args.Players.Select(FramePlayer.Get).ToList();
        }
        #endregion

        public virtual bool Check(FramePlayer player, out CustomRolePlusData data)
        {
            return BaseData.TryGetValue(player, out data);
        }
        public override void AddRole(Player player)
        {
            AddRole(player.ToFPlayer());
        }

        public virtual void AddRole(FramePlayer fPlayer)
        {
            if (Check(fPlayer.ExPlayer)) return;

            Log.Debug($"已添加{fPlayer.ExPlayer.Nickname}的{Name}({Id})角色");

            base.AddRole(fPlayer.ExPlayer);
            AddRoleData(fPlayer);
            fPlayer.CustomRolePlus = this;

            if (MoreAttributes.BaseMovementSpeedMultiplier < 1f)
            {
                fPlayer.ExPlayer.EnableEffect(Exiled.API.Enums.EffectType.Disabled);
                fPlayer.ExPlayer.ChangeEffectIntensity(Exiled.API.Enums.EffectType.Disabled, 1);
            }

            if (MoreAttributes.BaseMovementSpeedMultiplier > 1f)
            {
                fPlayer.ExPlayer.EnableEffect(Exiled.API.Enums.EffectType.MovementBoost);
                fPlayer.ExPlayer.ChangeEffectIntensity(Exiled.API.Enums.EffectType.MovementBoost, (byte)((MoreAttributes.BaseMovementSpeedMultiplier - 1f) * 100));
            }
            if (!string.IsNullOrEmpty(SpawnAttributes.Info)) Cassie.MessageTranslated($""/*ADMINISTER TEAM DESIGNATED {CASSIEDeathName} HASENTERED*/, SpawnAttributes.Info, true, true, true);
            if (!string.IsNullOrEmpty(SpawnAttributes.MusicFileName))
            {
                MusicManager.Instance.Play(SpawnAttributes.MusicFileName, "Spawn@localhost", $"{Name}", new MusicManager.TrackEvent());
            }
            fPlayer.UpdateShowInfoList();
        }
        public virtual void AddRoleData(FramePlayer fPlayer)
        {
            BaseData.Add(fPlayer, new CustomRolePlusData());
            if (this is ISkill skill)
            {
                SkillsManager skillsManager = new(fPlayer, skill);
                BaseData[fPlayer].SkillsManager = skillsManager;
            }
        }
        public override void RemoveRole(Player player)
        {
            RemoveRole(player.ToFPlayer());
        }

        public virtual void RemoveRole(FramePlayer fPlayer)
        {
            if (!Check(fPlayer)) return;
            Log.Debug($"已删除{fPlayer.ExPlayer.Nickname}的{Name}({Id})角色");
            if (Check(fPlayer, out CustomRolePlusData data) && !data.IsDeatHandling)
            {
                Cassie.MessageTranslated($"Died", $"{Name}游玩二游被榨干而死(非常正常死亡)");
            }
            base.RemoveRole(fPlayer.ExPlayer);
            BaseData.Remove(fPlayer);
            fPlayer.CustomRolePlus = null;
            NoCustomRole.Add(fPlayer);
            fPlayer.ExPlayer.ShowHint($"", 0.1f);
            fPlayer.HintManager.RoleText.Clear();
            fPlayer.UpdateShowInfoList();
        }
        #region TrySpawn
        private uint limitCount = 0;
        private uint spawnCount = 0;
        public virtual bool TrySpawn(FramePlayer fPlayer, bool chanceRef = false)
        {
            if (chanceRef)
            {
                limitCount = 0;
            }
            if (spawnCount < SpawnAttributes.MaxCount && Server.PlayerCount >= SpawnAttributes.MinPlayer && SpawnChanceNum <= SpawnAttributes.Chance && SpawnProperties.Limit > limitCount && fPlayer.ExPlayer.GetCustomRoles().Count == 0)
            {
                NoCustomRole.Remove(fPlayer);
                limitCount++;
                spawnCount++;
                AddRole(fPlayer);
                return true;
            }
            return false;
        }
        public virtual bool TrySpawn(List<FramePlayer> noCustomRole, bool chanceRef = false)
        {
            if (noCustomRole == null || noCustomRole.Count == 0) { return false; }
            return TrySpawn(noCustomRole[Loader.Random.StrictNext(0, noCustomRole.Count)]);
        }
        #endregion

        #region Events
        private void OnRestartingRound()
        {
            limitCount = 0;
            spawnCount = 0;
        }

        private void OnRoundStarted()
        {
            if (IStaetSpawn && SpawnAttributes.RefreshTeam == RefreshTeamType.Start)
            {
                TrySpawn(NoCustomRole.FindAll((p) => OldRole == RoleTypeId.None && Role == p.ExPlayer.Role.Type || p.ExPlayer.Role.Type == OldRole));
            }
        }


        private void OnSpawning(SpawningEventArgs args)
        {
            FramePlayer fPlayer = args.Player.ToFPlayer();
            if (RespawnTeamPlayer.Contains(fPlayer) && NoCustomRole.Contains(fPlayer)
                && IStaetSpawn && SpawnAttributes.StartWave <= RespawnWave
                && (OldRole == RoleTypeId.None && args.Player.Role.Type == OldRole))
            {
                TrySpawn(fPlayer);
            }
        }
        private void OnDroppingItem(DroppingItemEventArgs args)
        {
            FramePlayer fPlayer = args.Player.ToFPlayer();
            if (Check(fPlayer, out CustomRolePlusData data))
            {
                if (args.Item.Type == ItemType.Coin && data.SkillsManager != null)
                {
                    if (data.SkillsManager.IsActive)
                    {
                        fPlayer.HintManager.MessageTexts.Add(new HintManager.Text("技能正在持续", 5));
                    }
                    else if (data.SkillsManager.IsBurial)
                    {
                        fPlayer.HintManager.MessageTexts.Add(new HintManager.Text($"技能正在冷却(CD:{data.SkillsManager.BurialRemainingTime})", 5));
                    }
                    else
                    {
                        data.SkillsManager.Run(1);
                    }
                    args.IsAllowed = false;
                }
            }
        }
        private void OnHurting(HurtingEventArgs args)
        {
            if (args.Attacker != null && args.Player != null)
            {
                if (Check(args.Player))
                {
                    args.Amount *= MoreAttributes.DamageResistanceMultiplier;
                }
                else if (Check(args.Attacker))
                {
                    DamageHandler damageHandler = args.DamageHandler;
                    float damage = damageHandler.Damage * MoreAttributes.AttackDamageMultiplier;
                    if (MoreAttributes.IsAttackIgnoresArmor)
                    {
                        if (damageHandler is FirearmDamageHandler firearmDamageHandler)
                        {
                            damage += ((Exiled.API.Features.Roles.HumanRole)damageHandler.Target.Role).GetArmorEfficacy(firearmDamageHandler.Hitbox);
                        }
                    }
                    if (MoreAttributes.IsAttackIgnoresAhp)
                    {
                        damage += damageHandler.AbsorbedAhpDamage;
                    }
                    else
                    {
                        damageHandler.AbsorbedAhpDamage = 0;
                    }

                    if (damage < 0)
                    {
                        damageHandler.DealtHealthDamage = 0;
                    }
                    else
                    {
                        damageHandler.Damage = damage;
                    }
                }
            }
        }

        private void OnDying(DyingEventArgs args)
        {
            FramePlayer fPlayer = args.Player.ToFPlayer();
            if (Check(fPlayer, out CustomRolePlusData data))
            {
                if (args.Attacker == null)
                {
                    Cassie.MessageTranslated($"Died", $"{Name}被充满恶意的游戏环境草飞了");
                    data.IsDeatHandling = true;
                }
                else
                {
                    if (args.Attacker.GetCustomRoles().Count != 0)
                    {
                        CustomRole customRole = args.Attacker.GetCustomRoles()[0];
                        if (DeathText.TryGetValue(customRole.Id, out string text))
                        {
                            Cassie.MessageTranslated($"Died", text.Replace("{Name}", Name).Replace("{Attacker}", customRole.Name));
                        }
                        else
                        {
                            Cassie.MessageTranslated($"Died", $"({Name})被({customRole.Name})斩杀");
                        }
                    }
                    else
                    {
                        Cassie.MessageTranslated($"Died", $"({Name})被({args.Attacker.Nickname})斩杀");
                    }
                }
                data.IsDeatHandling = true;
            }
        }

        protected override void SubscribeEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted += new CustomEventHandler(OnRoundStarted);
            Exiled.Events.Handlers.Player.Spawning += new CustomEventHandler<SpawningEventArgs>(OnSpawning);
            Exiled.Events.Handlers.Player.Hurting += new CustomEventHandler<HurtingEventArgs>(OnHurting);
            Exiled.Events.Handlers.Server.RestartingRound += new CustomEventHandler(OnRestartingRound);
            Exiled.Events.Handlers.Player.DroppingItem += new CustomEventHandler<DroppingItemEventArgs>(OnDroppingItem);
            Exiled.Events.Handlers.Player.Dying += new CustomEventHandler<DyingEventArgs>(OnDying);
            base.SubscribeEvents();

            if (this is ISkill skill)
            {
                Inventory.Add(ItemType.Coin.ToString());
            }

        }
        protected override void UnsubscribeEvents()
        {
            Exiled.Events.Handlers.Server.RoundStarted -= new CustomEventHandler(OnRoundStarted);
            Exiled.Events.Handlers.Player.Hurting -= new CustomEventHandler<HurtingEventArgs>(OnHurting);
            Exiled.Events.Handlers.Server.RestartingRound -= new CustomEventHandler(OnRestartingRound);
            Exiled.Events.Handlers.Player.DroppingItem -= new CustomEventHandler<DroppingItemEventArgs>(OnDroppingItem);
            Exiled.Events.Handlers.Player.Spawning += new CustomEventHandler<SpawningEventArgs>(OnSpawning);
            Exiled.Events.Handlers.Player.Dying -= new CustomEventHandler<DyingEventArgs>(OnDying);
            base.UnsubscribeEvents();

            if (this is ISkill skill)
            {
                Inventory.Remove(ItemType.Coin.ToString());
            }
        }
        #endregion

        protected override void ShowMessage(Player player)
        {
            HintManager hintManager = player.ToFPlayer().HintManager;
            hintManager.RoleText.Clear();
            hintManager.RoleText.Add($"你是<b><color={NameColor}>{Name}</color></b>");
            hintManager.RoleText.Add($"{Description}");
        }

    }
    public abstract class CustomRolePlus<T> : CustomRolePlus where T : CustomRolePlusData, new()
    {
        public virtual bool Check(FramePlayer player, out T data)
        {
            if (BaseData.TryGetValue(player, out CustomRolePlusData baseData))
            {
                data = (T)baseData;
                return true;
            }
            data = null;
            return false;
        }

        public override void AddRoleData(FramePlayer fPlayer)
        {
            BaseData.Add(fPlayer, new T());
            if (this is ISkill skill)
            {
                SkillsManager skillsManager = new(fPlayer, skill);
                BaseData[fPlayer].SkillsManager = skillsManager;
            }
        }
    }
}