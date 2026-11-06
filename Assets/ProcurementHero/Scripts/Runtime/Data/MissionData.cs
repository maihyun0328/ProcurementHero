using System;
using System.Collections.Generic;
using ProcurementHero.Core;
using UnityEngine;

namespace ProcurementHero
{
    public enum FacilityKind
    {
        School,
        Clinic,
        Shelter
    }

    /// <summary>가상의 제안 제품 하나. 비용은 가상 게임 단위다.</summary>
    [Serializable]
    public class ProductEntry
    {
        public string id;
        public string displayName;
        public ProductType type;
        public int purchaseCost;
        public int maintenanceCost;
        public int performance;
        public int deliveryDays;
        public int ecoScore;
        [TextArea(1, 3)] public string summary;

        public int TotalCost => purchaseCost + maintenanceCost;

        public ProductSpec ToSpec()
        {
            return new ProductSpec(id, displayName, type, purchaseCost, maintenanceCost, performance, deliveryDays, ecoScore);
        }
    }

    [CreateAssetMenu(menuName = "Procurement Hero/Mission", fileName = "Mission")]
    public class MissionData : ScriptableObject
    {
        public string id;
        public string title;
        public FacilityKind facility;
        public string facilityName;
        [TextArea(2, 4)] public string request;

        public int budget;
        public int deadlineDays;
        public int targetPerformance;

        public string itemNameA;
        public string itemNameB;
        public List<ProductEntry> productsA = new List<ProductEntry>();
        public List<ProductEntry> productsB = new List<ProductEntry>();

        [TextArea(1, 3)] public string reactionSuccess;
        [TextArea(1, 3)] public string reactionPartial;
        [TextArea(1, 3)] public string reactionFail;

        public MissionSpec ToSpec()
        {
            return new MissionSpec(id, budget, deadlineDays, targetPerformance);
        }

        public List<ProductEntry> Products(int slot) => slot == 0 ? productsA : productsB;
        public string ItemName(int slot) => slot == 0 ? itemNameA : itemNameB;
    }
}
