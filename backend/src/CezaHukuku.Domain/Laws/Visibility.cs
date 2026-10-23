namespace CezaHukuku.Domain.Laws;

/// <summary>
/// Editoryal içeriğin görünürlüğü. Public içerik giriş yapmadan okunur; private içerik yalnız giriş yapmış
/// kullanıcılara görünür, anonim istekte hiç yokmuş gibi davranılır (404).
/// </summary>
public enum Visibility
{
    Public,
    Private,
}

public static class VisibilityExtensions
{
    /// <summary>
    /// Üst öğeden gelen görünürlüğü öğenin kendi ayarıyla birleştirir. Görünürlük yalnız daralır: üst öğe private
    /// ise alt öğe kendi ayarı ne olursa olsun private'tır (private bir kanunun maddesi anonim okuyucuya sızmasın).
    /// </summary>
    public static Visibility Narrow(this Visibility inherited, Visibility own) =>
        inherited == Visibility.Private || own == Visibility.Private ? Visibility.Private : Visibility.Public;
}
