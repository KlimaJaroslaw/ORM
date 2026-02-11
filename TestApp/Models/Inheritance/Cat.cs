namespace TestApp.Models.Inheritance;

public class Cat : Animal
{
    public int LivesRemaining { get; set; } = 9;

    public bool LikesLasers { get; set; } = true;
}
