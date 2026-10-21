using System;
using Okey101.Application;
using UnityEngine;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class PauseMenu : MonoBehaviour
    {
        [SerializeField] private Button resume,settings,leave;
        [SerializeField] private SettingsPanel settingsPanel;
        private GameBootstrap game;
        public bool SettingsOpen => settingsPanel!=null&&settingsPanel.IsOpen;
        public void BuildView()
        {
            MenuUI.Stretch((RectTransform)transform);
            var shade=TableUI.Panel(transform,"Pause shade",Vector2.zero,Vector2.one,new Color(0,0,0,.72f));
            var card=TableUI.Panel(shade,"Pause card",new Vector2(.30f,.24f),new Vector2(.70f,.77f));
            MenuUI.Text(card,"OYUN DURAKLATILDI",new Vector2(.05f,.77f),new Vector2(.95f,.94f),35);
            resume=TableUI.Button(card,"Devam et",new Vector2(.13f,.55f),new Vector2(.87f,.70f),null,30);
            settings=TableUI.Button(card,"Ayarlar",new Vector2(.13f,.33f),new Vector2(.87f,.48f),null,28);
            leave=TableUI.Button(card,"Menüye dön",new Vector2(.13f,.11f),new Vector2(.87f,.26f),null,28);
            var panel=new GameObject("Pause settings",typeof(RectTransform),typeof(SettingsPanel));panel.transform.SetParent(transform,false);
            settingsPanel=panel.GetComponent<SettingsPanel>();settingsPanel.BuildView();panel.SetActive(false);
        }
        public void Initialize(GameBootstrap owner)
        {
            game=owner;if(resume==null)BuildView();
            resume.onClick.AddListener(()=>game.SetPaused(false));
            settings.onClick.AddListener(()=>settingsPanel.Show(game.Settings,game.ApplySettings));
            leave.onClick.AddListener(Leave);gameObject.SetActive(false);
        }
        private async void Leave()
        {try{await game.RequestLeaveAsync();}catch(OperationCanceledException){}catch(Exception exception){if(this!=null)Debug.LogException(exception);}}
        public void SetVisible(bool value)
        {
            if(!value&&settingsPanel!=null)settingsPanel.Close();
            gameObject.SetActive(value);if(value){transform.SetAsLastSibling();ModalInputScope.Open(transform,resume);}
        }
        public void CloseSettings() {settingsPanel.Close();}
    }
}
