﻿using Exiled.API.Features;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.Features;
using MEC;
using System.Collections.Generic;
using System.Linq;
using YongAnFrame.Core.Data;
using YongAnFrame.Core.Manager;
using YongAnFrame.Events.EventArgs.FramePlayer;
using YongAnFrame.Role.Core;

namespace YongAnFrame.Core
{
    public class FramePlayer
    {
        private PlayerTitle usingTitles = null;
        private PlayerTitle usingRankTitles = null;
        private static readonly Dictionary<int, FramePlayer> dictionary = [];

        /// <summary>
        /// 拥有该实例的Exiled玩家
        /// </summary>
        public Player ExPlayer { get; private set; }
        /// <summary>
        /// 有效的框架玩家列表
        /// </summary>
        public static IReadOnlyCollection<FramePlayer> List => dictionary.Values.Where((p) => !p.IsInvalid).ToList();
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsInvalid { get => ExPlayer == null; }
        /// <summary>
        /// 实例拥有的自定义角色
        /// </summary>
        public CustomRolePlus CustomRolePlus { get; internal set; }
        /// <summary>
        /// 提示系统管理器
        /// </summary>
        public HintManager HintManager { get; private set; }
        /// <summary>
        /// 玩家等级
        /// </summary>
        public ulong Level { get; set; }

        /// <summary>
        /// 正在使用的名称称号
        /// </summary>
        public PlayerTitle UsingTitles { get => usingTitles; set { if (value != null && !value.IsRank) { usingTitles = value; } } }

        /// <summary>
        /// 正在使用的排名称号
        /// </summary>
        public PlayerTitle UsingRankTitles { get => usingRankTitles; set { if (value != null && value.IsRank) { usingRankTitles = value; } } }

        #region Static
        public static void SubscribeStaticEvents()
        {
            Exiled.Events.Handlers.Player.Verified += new CustomEventHandler<VerifiedEventArgs>(OnStaticVerified);
            Exiled.Events.Handlers.Server.WaitingForPlayers += new CustomEventHandler(OnStaticWaitingForPlayers);
            Exiled.Events.Handlers.Player.Destroying += new CustomEventHandler<DestroyingEventArgs>(OnStaticDestroying);
        }

        public static void UnsubscribeStaticEvents()
        {
            Exiled.Events.Handlers.Player.Verified += new CustomEventHandler<VerifiedEventArgs>(OnStaticVerified);
            Exiled.Events.Handlers.Server.WaitingForPlayers += new CustomEventHandler(OnStaticWaitingForPlayers);
            Exiled.Events.Handlers.Player.Destroying += new CustomEventHandler<DestroyingEventArgs>(OnStaticDestroying);
        }

        private static void OnStaticVerified(VerifiedEventArgs args)
        {
            new FramePlayer(args.Player);
        }
        private static void OnStaticDestroying(DestroyingEventArgs args)
        {
            FramePlayer fPlayer = args.Player.ToFPlayer();
            fPlayer.Invalid();
        }
        private static void OnStaticWaitingForPlayers()
        {
            dictionary.Clear();
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="player">Exiled玩家</param>
        internal FramePlayer(Player player)
        {
            ExPlayer = player;
            HintManager = new HintManager(this);
            dictionary.Add(ExPlayer.Id, this);
            Events.Handlers.FramePlayer.OnCreateFramePlayer(new CreateFramePlayerEventArgs(this));
        }

        #region ShowRank

        private readonly CoroutineHandle[] coroutines = new CoroutineHandle[2];

        internal void UpdateShowInfoList()
        {
            if (ExPlayer.IsNPC) return;

            if (ExPlayer.GlobalBadge != null)
            {
                ExPlayer.CustomName = $"[LV:{Level}][全球徽章]{ExPlayer.Nickname}";
                if (!string.IsNullOrEmpty(CustomRolePlus.Name))
                {
                    ExPlayer.RankName = $"*{ExPlayer.GlobalBadge.Value.Text}* {CustomRolePlus.Name}";
                }
                else
                {
                    ExPlayer.RankName = $"{ExPlayer.GlobalBadge.Value.Text}";
                }
                ExPlayer.RankColor = $"{ExPlayer.GlobalBadge.Value.Color}";
                return;
            }

            if (usingRankTitles != null)
            {
                if (usingRankTitles.DynamicCommand != null)
                {
                    Timing.KillCoroutines(coroutines[0]);
                    coroutines[0] = Timing.RunCoroutine(DynamicProTitlesShow(CustomRolePlus.NameColor));
                }
                else
                {
                    if (!string.IsNullOrEmpty(usingRankTitles.Color))
                    {
                        ExPlayer.RankColor = usingRankTitles.Color;
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(CustomRolePlus.NameColor))
                        {
                            ExPlayer.RankColor = CustomRolePlus.NameColor;
                        }
                        else
                        {
                            ExPlayer.RankColor = null;
                        }
                    }

                    if (!string.IsNullOrEmpty(CustomRolePlus.Name))
                    {
                        ExPlayer.RankName = $"{CustomRolePlus.Name} *{usingRankTitles.Name}*";
                    }
                    else
                    {
                        ExPlayer.RankName = usingRankTitles.Name;
                    }
                }
            }

            if (usingTitles != null)
            {
                if (usingTitles.DynamicCommand != null)
                {
                    Timing.KillCoroutines(coroutines[1]);
                    coroutines[1] = Timing.RunCoroutine(DynamicTitlesShow());
                }
                else
                {
                    ExPlayer.CustomName = $"[LV:{Level}][{usingTitles.Name}]{ExPlayer.Nickname}";
                    if (!string.IsNullOrEmpty(usingTitles.Color))
                    {
                        ExPlayer.RankColor = usingTitles.Color;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(CustomRolePlus.Name))
                {
                    ExPlayer.RankName = CustomRolePlus.Name;
                }
                if (!string.IsNullOrEmpty(CustomRolePlus.NameColor))
                {
                    ExPlayer.RankColor = CustomRolePlus.NameColor;
                }
            }

            if (usingTitles == null) ExPlayer.CustomName = $"[LV:{Level}]{ExPlayer.Nickname}";
        }

        private IEnumerator<float> DynamicProTitlesShow(string name = null)
        {
            while (true)
            {
                foreach (var command in usingRankTitles.DynamicCommand)
                {
                    if (name != null)
                    {
                        ExPlayer.RankName = $"{name ?? name} *{command[0]}*";
                    }
                    else
                    {
                        ExPlayer.RankName = $"{command[0]}";
                    }

                    ExPlayer.RankColor = command[1] != "null" ? command[1] : ExPlayer.RankColor;
                    yield return Timing.WaitForSeconds(float.Parse(command[2]));
                }
            }
        }
        private IEnumerator<float> DynamicTitlesShow()
        {
            while (true)
            {
                foreach (var command in usingTitles.DynamicCommand)
                {
                    ExPlayer.CustomName = $"[LV:{Level}][{command[0]}]{ExPlayer.Nickname}";
                    if (usingRankTitles == null)
                    {
                        ExPlayer.RankColor = command[1] != "null" ? command[1] : ExPlayer.RankColor;
                    }
                    yield return Timing.WaitForSeconds(float.Parse(command[2]));
                }
            }
        }
        #endregion

        /// <summary>
        /// 获取框架玩家
        /// </summary>
        /// <param name="player">Exiled玩家</param>
        /// <returns>框架玩家</returns>
        public static FramePlayer Get(Player player)
        {
            if (dictionary.TryGetValue(player.Id, out FramePlayer yPlayer))
            {
                return yPlayer;
            }
            return null;
        }
        /// <summary>
        /// 获取框架玩家
        /// </summary>
        /// <param name="numId">玩家数字ID</param>
        /// <returns>框架玩家</returns>
        public static FramePlayer Get(int numId)
        {
            return Get(Player.Get(numId));
        }

        /// <summary>
        /// 调用后该实例会立刻无效<br/>
        /// 调用后该实例会立刻无效<br/>
        /// 调用后该实例会立刻无效
        /// </summary>
        public void Invalid()
        {
            Events.Handlers.FramePlayer.OnInvalidFramePlayer(new InvalidFramePlayerEventArgs(this));
            HintManager?.Clean();
            ExPlayer = null;
        }

        public static implicit operator Player(FramePlayer yPlayer)
        {
            return yPlayer.ExPlayer;
        }
    }
}