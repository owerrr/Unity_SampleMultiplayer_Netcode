namespace SampleMultiplayer
{
    public static class ChatErrorMessage
    {
        public const string NotConnected = "[Błąd] Nie jesteś połączony z serwerem.";
        public const string CannotGetPlayerId = "[Błąd] Nie można pobrać Twojego ID gracza.";
        public const string InvalidSyntax = "[Błąd] Nieprawidłowy format. Użyj: /w <ID gracza> <wiadomość>";
        public const string InvalidPlayerId_LessThanZero = "[Błąd] Nieprawidłowy ID gracza. ID musi być liczbą większą od 0.";
        public const string InvalidPlayerId_SameAsSender = "[Błąd] Nie możesz wysłać prywatnej wiadomości do samego siebie.";
        public const string InvalidTarget = "[Błąd] Nieprawidłowy ID odbiorcy.";
        public const string SelfMessage = "[Błąd] Nie możesz wysłać prywatnej wiadomości do samego siebie.";
        public static string PlayerNotFound(int id) => $"[Błąd] Gracz o ID {id} nie istnieje lub nie jest połączony.";
    }
}