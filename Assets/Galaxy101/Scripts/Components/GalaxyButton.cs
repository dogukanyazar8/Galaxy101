using System;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Button text participates in layout even when decorated with absolute artwork.</summary>
    public sealed class GalaxyButton : Button
    {
        private readonly Label caption;
        private string currentText="";
        public GalaxyButton(Action action):base(action)
        {
            caption=new Label {name="button-caption",pickingMode=PickingMode.Ignore,enableRichText=false};
            caption.style.marginLeft=caption.style.marginRight=caption.style.marginTop=caption.style.marginBottom=0;
            caption.style.paddingLeft=caption.style.paddingRight=caption.style.paddingTop=caption.style.paddingBottom=0;
            caption.style.whiteSpace=WhiteSpace.NoWrap;caption.style.flexShrink=0;
            Add(caption);text=currentText;
        }
        public override string text
        {
            get=>currentText;
            set
            {
                currentText=value??"";if(caption==null)return;
                caption.text=currentText;caption.style.display=currentText.Length==0?DisplayStyle.None:DisplayStyle.Flex;
            }
        }
    }
}
