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

namespace StaTSpace
{
    public class StaTSoundController : MonoBehaviour
    {
        public static StaTSoundController Instance { get; private set; }   //シングルトン

        public const int SourceChannel = 8; //同時再生は8個

        public AudioSource[] LoopSources;
        public AudioClip LockingSound;
        public AudioClip LockedSound;

        public AudioSource audioSourceOneShot;

        public void Awake()
        {
            Instance = this;

            LockingSound = ModAudioClip.GetAudioClip("Locking");
            LockedSound = ModAudioClip.GetAudioClip("Locked");

            audioSourceOneShot = gameObject.AddComponent<AudioSource>();
            audioSourceOneShot.playOnAwake = false;
            audioSourceOneShot.spatialBlend = 0f;

            LoopSources = InitializeAudioSources();
        }
        /// <summary>
        /// お手本のAudioSourceの設定を、配列の全要素にコピーして初期化する
        /// </summary>
        public AudioSource[] InitializeAudioSources()
        {
            AudioSource[] audioSources = new AudioSource[SourceChannel];

            for (int i = 0; i < SourceChannel; i++)
            {
                // 新しいAudioSourceをこのGameObjectに追加
                AudioSource src = gameObject.AddComponent<AudioSource>();

                // お手本の設定を1つ1つコピーする
                CopyAudioSourceSettings(audioSourceOneShot, src);

                audioSources[i] = src;
            }

            return audioSources;
        }

        /// <summary>
        /// AudioSourceの設定を、fromからtoへコピーする関数
        /// </summary>
        public void CopyAudioSourceSettings(AudioSource from, AudioSource to)
        {
            to.playOnAwake = from.playOnAwake;  // 使い回すので自動再生はオフにする
            to.spatialBlend = from.spatialBlend;
        }

        /// <summary>
        /// Locked音を鳴らす
        /// </summary>
        public void PlayLocked()
        {
            audioSourceOneShot.PlayOneShot(LockedSound);
        }

        /// <summary>
        /// Locking音（ループ）を新たに1つ鳴らす
        /// 空いているソースを探して再生する
        /// </summary>
        public void PlayLocking()
        {
            // 空いている（再生中でない）ソースを探す
            for (int i = 0; i < SourceChannel; i++)
            {
                if (!LoopSources[i].isPlaying)
                {
                    LoopSources[i].clip = LockingSound;
                    LoopSources[i].loop = true;   // ループ再生
                    LoopSources[i].Play();
                    return;
                }
            }

            // 全て使用中の場合は鳴らさない
        }

        /// <summary>
        /// 再生中のもののうち、一番番号が若い（インデックスが小さい）ものを止める関数
        /// </summary>
        public void StopLocking()
        {
            // インデックスの小さい順に見て、最初に見つかった再生中のものを止める
            for (int i = 0; i < SourceChannel; i++)
            {
                if (LoopSources[i].isPlaying)
                {
                    LoopSources[i].Stop();
                    return;  // 1つ止めたら終了
                }
            }

            // 再生中のものが無ければ何もしない
        }

        /// <summary>
        /// 音量を変える関数
        /// </summary>
        public void ChangeVolume(float volume)
        {
            audioSourceOneShot.volume = volume;

            for (int i = 0; i < SourceChannel; i++)
            {
                LoopSources[i].volume = volume;
            }
        }

        /// <summary>
        /// 全てのロック準備音を止める関数
        /// </summary>
        public void StopAllSound()
        {
            for (int i = 0; i < SourceChannel; i++)
            {
                if (LoopSources[i].isPlaying)
                {
                    LoopSources[i].Stop();
                }
            }
        }
    }
}
