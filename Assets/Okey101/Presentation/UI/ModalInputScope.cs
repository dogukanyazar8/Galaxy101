using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    internal sealed class ModalInputScope : MonoBehaviour
    {
        private readonly List<(CanvasGroup group,bool interactable)> blocked = new List<(CanvasGroup,bool)>();
        private GameObject previousSelection;
        private bool opened;

        internal static void Open(Transform modal,Selectable first)
        {
            var scope=modal.GetComponent<ModalInputScope>();
            if(scope==null)scope=modal.gameObject.AddComponent<ModalInputScope>();
            if(scope.opened)return;
            scope.opened=true;
            var events=EventSystem.current;
            scope.previousSelection=events!=null?events.currentSelectedGameObject:null;
            var canvas=modal.GetComponentInParent<Canvas>();
            for(var branch=modal;canvas!=null&&branch!=canvas.rootCanvas.transform;branch=branch.parent)
                foreach(Transform sibling in branch.parent)
                {
                    if(sibling==branch||!sibling.gameObject.activeInHierarchy)continue;
                    var group=sibling.GetComponent<CanvasGroup>();
                    if(group==null)group=sibling.gameObject.AddComponent<CanvasGroup>();
                    scope.blocked.Add((group,group.interactable));group.interactable=false;
                }
            if(events!=null&&first!=null)events.SetSelectedGameObject(first.gameObject);
        }

        private void OnDisable()
        {
            if(!opened)return;opened=false;
            foreach(var entry in blocked)if(entry.group!=null)entry.group.interactable=entry.interactable;
            blocked.Clear();
            if(EventSystem.current!=null&&previousSelection!=null&&previousSelection.activeInHierarchy)
            {
                var selectable=previousSelection.GetComponent<Selectable>();
                if(selectable!=null&&selectable.IsInteractable())EventSystem.current.SetSelectedGameObject(previousSelection);
            }
            previousSelection=null;
        }
    }
}
