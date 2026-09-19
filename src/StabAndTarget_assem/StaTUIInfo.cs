using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace StaTSpace
{
    public static class StaTUIInfo
    {
        public static Dictionary<MPTeam, Color> TeamColors = new Dictionary<MPTeam, Color>
        {
            {MPTeam.None,   new Color(0.8f, 0.8f, 0.8f)},
            {MPTeam.Red,    Color.red},
            {MPTeam.Green,  new Color(0.2f, 0.8f, 0.2f)},
            {MPTeam.Orange, new Color(1.0f, 0.6f, 0.1f)},
            {MPTeam.Blue,   new Color(0.2f, 0.6f, 1.0f)},
        };

        public static Dictionary<StaTLockState, Color> LockStateColors = new Dictionary<StaTLockState, Color>
        {
            {StaTLockState.None, new Color(0.8f, 0.8f, 0.8f)},
            {StaTLockState.Primary, new Color(0.2f, 0.8f, 0.2f)},
            {StaTLockState.Secondary, Color.red}
        };

        public static Color AlertColor = Color.red;
        public static Color FriendColor = new Color(0.2f, 0.6f, 1.0f);
    }
}
