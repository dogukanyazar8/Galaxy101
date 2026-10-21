using Okey101.Core.Model;
using UnityEngine;
using System;
using System.Linq;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class ResultPanel : MonoBehaviour
    {
        [SerializeField] private Text summary;
        [SerializeField] private Button next,menu;
        private GameBootstrap game;
        public string Summary {get;private set;}="";
        public void BuildView()
        {
            MenuUI.Stretch((RectTransform)transform);
            var shade=TableUI.Panel(transform,"Result shade",Vector2.zero,Vector2.one,new Color(0,0,0,.74f));
            var card=TableUI.Panel(shade,"Result card",new Vector2(.20f,.24f),new Vector2(.80f,.84f));
            summary=MenuUI.Text(card,"Round result",new Vector2(.04f,.25f),new Vector2(.96f,.96f),25);
            next=TableUI.Button(card,"Sonraki el",new Vector2(.06f,.06f),new Vector2(.48f,.19f),null,28);
            menu=TableUI.Button(card,"Menüye dön",new Vector2(.52f,.06f),new Vector2(.94f,.19f),null,28);
        }
        public void Initialize(GameBootstrap owner)
        {game=owner;if(summary==null)BuildView();next.onClick.AddListener(Next);menu.onClick.AddListener(Menu);gameObject.SetActive(false);}
        public void Show(RoundResult result)
        {
            if(result==null){gameObject.SetActive(false);return;}
            var reason=result.FinishInfo.Reason;
            var title=reason==FinishReason.AllPairs?"Dört oyuncu çift açtı. El iptal edildi.\nPuan yazılmadı."
                :reason==FinishReason.StockExhausted?"Deste bitti.":$"Oyuncu {result.FinishInfo.Winner+1} bitirdi.";
            Summary=title+"\n\n"+string.Join("\n",result.Scores.Select((score,i)=>
                $"Oyuncu {i+1}  ·  {(score.HasScore?"Toplam: "+score.Total:"Puan yazılmadı")}  [N={score.N}, J={score.J}, P={score.P}, B={score.B}, M={score.M}]"))
                +"\n\nN: normal taşlar · J: okey adedi · P: tur cezaları\nB: taban puan · M: bitiş çarpanı";
            if(summary!=null)
            {summary.text=Summary;next.GetComponentInChildren<Text>().text=reason==FinishReason.AllPairs?"Yeniden dağıt":"Sonraki el";gameObject.SetActive(true);ModalInputScope.Open(transform,next);}
        }
        public void SetActionsEnabled(bool value){if(next!=null){next.interactable=value;menu.interactable=value;}}
        private async void Next()
        {try{await game.StartNextRoundAsync();}catch(OperationCanceledException){}catch(Exception exception){if(this!=null)Debug.LogException(exception);}}
        private async void Menu()
        {try{await game.RequestLeaveAsync();}catch(OperationCanceledException){}catch(Exception exception){if(this!=null)Debug.LogException(exception);}}
    }
}
