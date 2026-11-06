using System.Collections.Generic;
using UnityEngine;

namespace ProcurementHero
{
    /// <summary>미션 순서를 정하는 단일 진입점. Setup Game이 생성하고 GameManager가 참조한다.</summary>
    [CreateAssetMenu(menuName = "Procurement Hero/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        public List<MissionData> missions = new List<MissionData>();
    }
}
