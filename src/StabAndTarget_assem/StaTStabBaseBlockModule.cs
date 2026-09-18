using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using Modding;
using Modding.Modules;
using Modding.Serialization;
using Modding.Blocks;
using Modding.Common;
using UnityEngine;
using UnityEngine.UI;
using Localisation;

namespace StaTSpace
{
    //XML上での名前
    [XmlRoot("StaTStabBaseBlockModule")]
    [Reloadable]

    public class StaTStabBaseBlockModule : BlockModule
    {
        [XmlElement("StabIDSlider")]
        [RequireToValidate]
        public MSliderReference StabIDSlider;
    }

    public class StaTStabBaseBlockModuleBehaviour : BlockModuleBehaviour<StaTStabBaseBlockModule>
    {
        public MSlider StabIDSlider;
        public int stabID;
        public int BlockPlayerID;

        ///<summary>
        ///シミュ開始時、ID, ブロックの持ち主のID, BlockBehaviourを揃って登録
        /// </summary>
        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            StabIDSlider = GetSlider(Module.StabIDSlider);
            stabID = (int)StabIDSlider.Value;
            
            //BlockPlayerIDを取得
            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;

            //ID登録命令、こちらは一意に定まるためホストクライアント両方が行い、通信等は行わない
            StatTStabIDContoroller.RegisterStabBase(BlockBehaviour, stabID, BlockPlayerID);
        }

        //シミュ停止時かつリスポ等でない⇒辞書を削除
        public override void OnSimulateStop()
        {
            base.OnSimulateStop();

            if(!StatMaster.levelSimulating)
            {
                StatTStabIDContoroller.ClearDictionary();
            }
        }
    }
}
