using QFramework;

namespace EachOneCoin
{
    public interface IGameModel : IModel
    {
    }

    public sealed class GameModel : AbstractModel, IGameModel
    {
        protected override void OnInit()
        {
        }
    }
}
