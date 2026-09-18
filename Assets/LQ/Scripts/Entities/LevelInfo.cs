using System.Collections.Generic;
using UnityEngine;

namespace LQ {
    public class LevelInfo : MonoBehaviour {
        public static LevelInfo Current;
        public string mapName, message; public int worldtype; public int monsterCount, secretCount;
        public static int WorldType => Current != null ? Current.worldtype : 0;
        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }
    }
}
