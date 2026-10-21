using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using System;

namespace Okey101.Presentation
{
    public sealed class LeaveRoundDialog : MonoBehaviour
    {
        private TaskCompletionSource<bool> pending;
        private bool built;
        private Button cancel;
        public bool IsOpen => pending!=null;
        public void Initialize(Transform canvas)
        {
            if(built)return;built=true;
            var root=(RectTransform)transform;root.SetParent(canvas,false);MenuUI.Stretch(root);
            var shade=TableUI.Panel(root,"Leave shade",Vector2.zero,Vector2.one,new Color(0,0,0,.80f));
            var card=TableUI.Panel(shade,"Leave card",new Vector2(.28f,.33f),new Vector2(.72f,.69f));
            MenuUI.Text(card,"MASADAN AYRIL",new Vector2(.06f,.73f),new Vector2(.94f,.94f),36);
            MenuUI.Text(card,"Bu el kaydedilmez.\nMenüye dönmek istiyor musunuz?",new Vector2(.06f,.32f),new Vector2(.94f,.72f),28);
            cancel=TableUI.Button(card,"Vazgeç",new Vector2(.08f,.08f),new Vector2(.48f,.25f),()=>Resolve(false),26);
            TableUI.Button(card,"Menüye dön",new Vector2(.52f,.08f),new Vector2(.92f,.25f),()=>Resolve(true),26);
            gameObject.SetActive(false);
        }
        public async Task<bool> ConfirmAsync(CancellationToken token)
        {
            if(pending!=null)throw new InvalidOperationException("A leave confirmation is already open.");
            token.ThrowIfCancellationRequested();
            var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);pending=completion;
            gameObject.SetActive(true);transform.SetAsLastSibling();
            ModalInputScope.Open(transform,cancel);
            try{using(token.Register(()=>completion.TrySetCanceled()))return await completion.Task;}
            finally{pending=null;if(this!=null)gameObject.SetActive(false);}
        }
        public void Resolve(bool accepted) {pending?.TrySetResult(accepted);}
        private void OnDisable(){pending?.TrySetCanceled();}
        private void OnDestroy(){pending?.TrySetCanceled();}
    }
}
