using System;
using System.IO;
using Okey101.Application;
using UnityEngine;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private Slider volume;
        [SerializeField] private Toggle fullscreen,reduced;
        [SerializeField] private Text volumeLabel,error;
        [SerializeField] private Button save,cancel;
        private Action<LocalSettings> onSaved;
        private bool initialized;
        public bool IsOpen => gameObject.activeSelf;
        public void BuildView()
        {
            MenuUI.Stretch((RectTransform)transform);
            var shade=TableUI.Panel(transform,"Settings shade",Vector2.zero,Vector2.one,new Color(0,0,0,.78f));
            var card=TableUI.Panel(shade,"Settings card",new Vector2(.27f,.22f),new Vector2(.73f,.80f));
            MenuUI.Text(card,"AYARLAR",new Vector2(.06f,.83f),new Vector2(.94f,.96f),40);
            volumeLabel=MenuUI.Text(card,"Ana ses",new Vector2(.09f,.69f),new Vector2(.91f,.79f));
            volume=MenuUI.VolumeSlider(card,new Vector2(.12f,.57f),new Vector2(.88f,.68f));
            fullscreen=MenuUI.Toggle(card,"Tam ekran",new Vector2(.12f,.42f),new Vector2(.88f,.53f));
            reduced=MenuUI.Toggle(card,"Azaltılmış hareket",new Vector2(.12f,.28f),new Vector2(.88f,.39f));
            error=MenuUI.Text(card,"",new Vector2(.06f,.18f),new Vector2(.94f,.27f),22);
            save=TableUI.Button(card,"Kaydet",new Vector2(.09f,.04f),new Vector2(.48f,.15f),null,26);
            cancel=TableUI.Button(card,"Vazgeç",new Vector2(.52f,.04f),new Vector2(.91f,.15f),null,26);
        }
        public void Show(LocalSettings current,Action<LocalSettings> saved)
        {
            if(initialized&&IsOpen)return;
            if(volume==null)BuildView();
            if(!initialized)
            {
                initialized=true;save.onClick.AddListener(Save);cancel.onClick.AddListener(Close);
                volume.onValueChanged.AddListener(value=>volumeLabel.text=$"Ana ses  %{Mathf.RoundToInt(value*100)}");
            }
            onSaved=saved;volume.SetValueWithoutNotify(current.MasterVolume);volumeLabel.text=$"Ana ses  %{Mathf.RoundToInt(current.MasterVolume*100)}";
            fullscreen.SetIsOnWithoutNotify(current.Fullscreen);reduced.SetIsOnWithoutNotify(current.ReducedMotion);error.text="";
            gameObject.SetActive(true);transform.SetAsLastSibling();
            ModalInputScope.Open(transform,volume);
        }
        private void Save()
        {
            var settings=new LocalSettings {MasterVolume=volume.value,Fullscreen=fullscreen.isOn,ReducedMotion=reduced.isOn};
            try { MenuUI.SaveSettings(settings);MenuUI.Apply(settings);onSaved?.Invoke(settings);Close(); }
            catch(Exception exception) when(exception is IOException||exception is UnauthorizedAccessException)
            {Debug.LogWarning("Settings save failed: "+exception);error.text="Ayarlar kaydedilemedi. Yeniden deneyin.";}
        }
        public void Close() { gameObject.SetActive(false); }
    }
}
