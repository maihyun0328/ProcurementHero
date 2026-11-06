using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProcurementHero
{
    /// <summary>키보드로 선택된 요소에 남색 테두리를 보여 준다.</summary>
    [RequireComponent(typeof(Graphic))]
    public class FocusRing : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        Outline outline;

        void Awake()
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Theme.Navy;
            outline.effectDistance = new Vector2(4, -4);
            outline.useGraphicAlpha = false;
            outline.enabled = false;
        }

        public void OnSelect(BaseEventData eventData) => outline.enabled = true;
        public void OnDeselect(BaseEventData eventData) => outline.enabled = false;
        void OnDisable() { if (outline) outline.enabled = false; }
    }
}
