namespace ToyKingdom
{
    public class BattleResult
    {
        public string cityId;
        public string cityName;
        public bool victory;
        public int stars;
        public int goldEarned;
        public int squadsLost;
    }

    /// <summary>Sahneler arasında taşınan geçici bilgiler.</summary>
    public static class GameSession
    {
        public static CastleData SelectedCastle;
        public static BattleResult LastResult;
    }
}
