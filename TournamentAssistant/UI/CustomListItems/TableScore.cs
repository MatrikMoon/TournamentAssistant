using System;

[Serializable]
public class TableScore
{
    public ulong UserId { get; set; }
    public string Username { get; set; }
    public int Score { get; set; }
    public bool FullCombo { get; set; }
    public string Color { get; set; }
    public string TextColor { get; set; }
    public int ScoreboardPosition { get; set; }
    public string bgColor { get; set; } //preparation for team colors, player background will be used
}