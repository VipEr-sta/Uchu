namespace Uchu.World
{
    public class TeamPlayerComponent : Component
    {
        public void MessageSetLeader(Player player)
        {
            var @this = (Player) GameObject;
            
            @this.Message(new TeamSetLeaderMessage
            {
                Associate = GameObject,
                NewLeader = player
            });
        }

        public void MessageAddPlayer(Player player)
        {
            var @this = (Player) GameObject;
            
            @this.Message(new TeamAddPlayerMessage
            {
                Associate = GameObject,
                Player = player
            });
        }
    }
}