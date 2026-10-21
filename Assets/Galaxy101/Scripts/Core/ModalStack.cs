using System;
using System.Collections.Generic;

namespace Galaxy101
{
    public sealed class ModalStack
    {
        private static readonly HashSet<string> Panels=new HashSet<string>{"settings","shop","friends","leader","daily","rules","me"};
        private static readonly HashSet<string> Dialogs=new HashSet<string>{"profile","logout","delete","bye","add","share"};
        public string Panel {get;private set;}
        public string Dialog {get;private set;}
        public void OpenPanel(string panel)
        {
            if(!Panels.Contains(panel))throw new ArgumentOutOfRangeException(nameof(panel));
            Panel=panel;Dialog=null;
        }
        public void OpenDialog(string dialog)
        {
            if(!Dialogs.Contains(dialog))throw new ArgumentOutOfRangeException(nameof(dialog));
            Dialog=dialog;
        }
        public void CloseTop(){if(Dialog!=null)Dialog=null;else Panel=null;}
    }
}
