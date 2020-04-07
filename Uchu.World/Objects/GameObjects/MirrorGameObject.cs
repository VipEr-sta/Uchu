using System.Numerics;
using System.Threading.Tasks;
using Uchu.World.Filters;

namespace Uchu.World
{
    public class MirrorGameObject : AuthoredGameObject
    {
        protected MirrorGameObject()
        {
            Listen(OnStart, () =>
            {
                var player = (Player) Author;

                Listen(player.OnPositionUpdate, MimicMovement);

                return Task.CompletedTask;
            });
            
            Listen(OnDestroyed, () =>
            {
                foreach (var player in Author.Zone.Players)
                {
                    if (player == Author) continue;
                    
                    var otherFilter = player.Perspective.GetFilter<ExcludeFilter>();

                    otherFilter.Include(Author);
                }
                
                return Task.CompletedTask;
            });
        }
        
        public static async Task<MirrorGameObject> InstantiateAsync(int lot, Player author)
        {
            var gameObject = await InstantiateAsync<MirrorGameObject>(
                author.Zone,
                lot,
                author.Transform.Position,
                author.Transform.Rotation
            );

            gameObject.Author = author;

            var filter = author.Perspective.GetFilter<ExcludeFilter>();

            filter.Exclude(gameObject);

            foreach (var player in author.Zone.Players)
            {
                if (player == author) continue;
                
                var otherFilter = player.Perspective.GetFilter<ExcludeFilter>();

                otherFilter.Exclude(author);
            }

            return gameObject;
        }

        private Task MimicMovement(Vector3 position, Quaternion rotation)
        {
            var controller = GetComponent<ControllablePhysicsComponent>();
            var authorController = Author.GetComponent<ControllablePhysicsComponent>();

            if (controller != default)
            {
                controller.HasPosition = authorController.HasPosition;
                controller.HasVelocity = authorController.HasVelocity;
                controller.Velocity = authorController.Velocity;

                controller.HasPosition = authorController.HasPosition;
                controller.IsOnGround = authorController.IsOnGround;

                controller.HasAngularVelocity = authorController.HasAngularVelocity;
                controller.AngularVelocity = authorController.AngularVelocity;

                controller.NegativeAngularVelocity = authorController.NegativeAngularVelocity;
            }

            Transform.Position = position;
            Transform.Rotation = rotation;

            if (TryGetComponent<Stats>(out var stats))
            {
                stats.Factions = new int[0];
            }

            Serialize(this);
            
            return Task.CompletedTask;
        }
    }
}