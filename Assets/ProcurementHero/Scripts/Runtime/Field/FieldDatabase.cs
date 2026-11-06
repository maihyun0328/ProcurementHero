using System.Collections.Generic;
using UnityEngine;

namespace ProcurementHero.Field
{
    /// <summary>현장 수첩 버전의 미션 순서. Setup Field Notebook이 만들고 FieldGame이 참조한다.</summary>
    [CreateAssetMenu(menuName = "Procurement Hero/Field Database", fileName = "FieldDatabase")]
    public class FieldDatabase : ScriptableObject
    {
        public List<FieldMissionData> missions = new List<FieldMissionData>();
    }
}
