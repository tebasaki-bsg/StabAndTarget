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
using Vector3 = UnityEngine.Vector3;
using UnityEngine.UI;
using Localisation;
using UnityEngine.Rendering;

namespace StaTSpace
{
    //XML上での名前
    [XmlRoot("StaTStabSlaveBlockModule")]
    [Reloadable]

    public class StaTStabSlaveBlockModule : BlockModule
    {
        [XmlElement("MasterIDSlider")]
        [RequireToValidate]
        public MSliderReference MasterIDSlider;

        [XmlElement("DamperSlider")]
        [RequireToValidate]
        public MSliderReference DamperSlider;

        [XmlElement("ActivateKey")]
        [RequireToValidate]
        public MKeyReference ActivateKey;
    }

    public class StaTStabSlaveBlockModuleBehaviour : BlockModuleBehaviour<StaTStabSlaveBlockModule>
    {
        public MSlider MasterIDSlider;
        public int masterID;

        public MSlider PowerSlider;
        public float power;
        public MSlider DamperSlider;
        public float damper;

        public MKey ActivateKey;

        public int BlockPlayerID;
        public Rigidbody rigidbody;

        public bool init = false;

        public string MasterIDString;
        public Transform MasterTransform;
        public Rigidbody MasterRigidbody;

        public override void SafeAwake()
        {
            base.SafeAwake();

            //ブロックの持ち主のID
            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            //各値を取得
            MasterIDSlider = GetSlider(Module.MasterIDSlider);
            masterID = (int)MasterIDSlider.Value;

            DamperSlider = GetSlider(Module.DamperSlider);
            damper = DamperSlider.Value;

            ActivateKey = GetKey(Module.ActivateKey);

            MasterIDString = masterID.ToString() + "_" + BlockPlayerID.ToString();

            rigidbody = GetComponent<Rigidbody>();
            rigidbody.angularDrag = damper;
            rigidbody.inertiaTensor = new Vector3(10f, 10f, 10f);
        }

        public override void SimulateFixedUpdateHost()
        {
            base.SimulateFixedUpdateHost();

            if(!init)
            {
                //Masterを探す

                MasterTransform = Mod.StabBaseDict[MasterIDString];
                MasterRigidbody = MasterTransform.gameObject.GetComponent<Rigidbody>();

                init = true;
            }

            //親が存在しない or 左右どちらかのモーターがどこにも接続していない
            if (MasterTransform == null)
            {
                return;
            }

            if(ActivateKey.IsHeld || ActivateKey.EmulationHeld())
            {
                rigidbody.rotation = MasterTransform.rotation;
            }
        }

        public override void OnSimulateStop()
        {
            base.OnSimulateStop();
        }
    }
}
