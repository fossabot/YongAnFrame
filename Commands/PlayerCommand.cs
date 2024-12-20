﻿using CommandSystem;
using Exiled.API.Features;
using System;
using YongAnFrame.Players;

namespace YongAnFrame.Commands
{
    [CommandHandler(typeof(ClientCommandHandler))]
    public class PlayerCommand : ICommand
    {
        public string Command => "hPlayer";

        public string[] Aliases => ["hPlay", "hp", "h"];

        public string Description => "用于管理自己的YongAnFrame用户";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            response = "NULL";
            if (arguments.Count < 2)
            {
                switch (arguments.Array[1])
                {
                    case "BDNT":
                        if (Player.TryGet(sender, out Player player))
                        {
                            FramePlayer fPlayer = FramePlayer.Get(player);
                            fPlayer.HintManager.Clean();
                            fPlayer.ExPlayer.ShowHint($"<size=20>{YongAnFramePlugin.Instance.Translation.BypassDoNotTrack.Split('\n')}</size>", 10000f);
                        }
                        return true;
                }

            }
            return false;
        }
    }
}
