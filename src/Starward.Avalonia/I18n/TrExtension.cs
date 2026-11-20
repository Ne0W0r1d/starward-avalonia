using Avalonia.Markup.Xaml;
using System;

namespace Starward.I18n;

public sealed class TrExtension : MarkupExtension
{

    public string Key { get; set; } = "";


    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Localization.Get(Key);
    }

}
