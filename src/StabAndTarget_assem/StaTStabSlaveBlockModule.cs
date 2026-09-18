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
    [XmlRoot("StaTStabSlaveBlockModule")]
    [Reloadable]

    public class StaTStabSlaveBlockModule : BlockModule
    {
        [XmlElement("MasterIDSlider")]
        [RequireToValidate]
        public MSliderReference MasterIDSlider;

        [XmlElement("PowerSlider")]
        [RequireToValidate]
        public MSliderReference PowerSlider;

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
        public MKey ActivateKey;

        public MToggle AppearPDConfigToggle;

        public MSlider ProportionalSlider;
        public float proportional;
        public MSlider DerivativeSlider;
        public float derivative;

        public int BlockPlayerID;
        public Rigidbody rigidbody;

        public bool init = false;

        public string MasterIDString;
        public Transform MasterTransform;
        public Rigidbody MasterRigidbody;

        public Quaternion CorrectionQuaternion;
        public UnityEngine.Vector3 CorrectionVector;
        public UnityEngine.Vector3 CorrectionTorque;

        public override void SafeAwake()
        {
            base.SafeAwake();

            //P成分・D成分を調整する用のトグルを作る
            AppearPDConfigToggle = BlockBehaviour.AddToggle(Mod.isJapanese ? "設定を変更" : "PD config", "pd-config", false);
            AppearPDConfigToggle.DisplayInMapper = true;
            AppearPDConfigToggle.Toggled += AppearPDConfig;

            //P成分・D成分用のスライダー
            ProportionalSlider = BlockBehaviour.AddSlider(Mod.isJapanese ? "P成分" : "Propotional", "propotional", 10f, 0.0f, 1000f);
            ProportionalSlider.DisplayInMapper = false;
            DerivativeSlider = BlockBehaviour.AddSlider(Mod.isJapanese ? "D成分" : "Derivative", "derivative", 9f, 0.0f, 1000f);
            DerivativeSlider.DisplayInMapper = false;

            //ブロックの持ち主のID
            BlockPlayerID = BlockBehaviour.ParentMachine.PlayerID;
        }

        public override void OnSimulateStart()
        {
            base.OnSimulateStart();

            //各値を取得
            MasterIDSlider = GetSlider(Module.MasterIDSlider);
            masterID = (int)MasterIDSlider.Value;

            PowerSlider = GetSlider(Module.PowerSlider);
            power = PowerSlider.Value;

            ActivateKey = GetKey(Module.ActivateKey);

            if (AppearPDConfigToggle.IsActive)
            {
                proportional = ProportionalSlider.Value;
                derivative = DerivativeSlider.Value;
            }
            else
            {
                proportional = power;
                derivative = power * 0.9f;
            }

            MasterIDString = masterID.ToString() + "_" + BlockPlayerID.ToString();

            rigidbody = GetComponent<Rigidbody>();
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

            if (MasterTransform == null)
            {
                return;
            }

            if(ActivateKey.IsHeld || ActivateKey.EmulationHeld())
            {
                //誤差クォータニオン（回転） = 目標角度×現在角度^-1
                CorrectionQuaternion = MasterTransform.rotation * Quaternion.Inverse(transform.rotation);

                //誤差クォータニオンを回転軸と回転量に分解
                CorrectionQuaternion.ToAngleAxis(out float CorrectionAngle, out UnityEngine.Vector3 CorrectionAxis);

                if (CorrectionAngle > 180f)
                {
                    CorrectionAngle -= 360f;
                }

                if (Mathf.Abs(CorrectionAngle) > Mathf.Epsilon)
                {
                    //誤差ベクトル（回転）＝軸(axis)×（角度(angle)のラジアン化）
                    CorrectionVector = CorrectionAxis * (CorrectionAngle * Mathf.Deg2Rad);

                    //修正用ベクトル = 誤差ベクトル×P - 相対回転速度×D
                    CorrectionTorque = (CorrectionVector * proportional) - ((rigidbody.angularVelocity - MasterRigidbody.angularVelocity) * derivative);

                    rigidbody.AddTorque(CorrectionTorque * power, ForceMode.Acceleration);
                }
            }

            
        }

        public void AppearPDConfig(bool value)
        {
            ProportionalSlider.DisplayInMapper = value;
            DerivativeSlider.DisplayInMapper = value;
        }
    }
}
