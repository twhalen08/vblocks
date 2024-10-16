using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VpNet;

namespace vblocks
{
    internal class Bot
    {
        private VirtualParadiseClient client;
        double cellSize = 0.100; // Size of each cube
        private HashSet<Vector3> occupiedPositions;
        private Dictionary<int, PlayerState> playerStates;
        private readonly Dictionary<string, string> textureMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    { "brick", "sw-brick16a" },
    { "stone", "sw-stone2" },
    { "wood", "sw-wood14" },
    { "dirt", "sw-ground5" },
    {"greywood", "sw-greywood1" }
    // Add more texture mappings as needed
};


        public Bot()
        {
            client = new VirtualParadiseClient();
            client.ObjectClicked += Client_ObjectClicked;
            client.ObjectCreated += Client_ObjectCreated;
            client.ObjectDeleted += Client_ObjectDeleted;
            client.ChatMessageReceived += Client_ChatMessageReceived;
            occupiedPositions = new HashSet<Vector3>(new Vector3Comparer(1e-4));
            playerStates = new Dictionary<int, PlayerState>();
        }

        private void Client_ChatMessageReceived(VirtualParadiseClient sender, ChatMessageEventArgs args)
        {
            string message = args.ChatMessage.Message;
            int userId = args.Avatar.User.Id;

            // Ensure the player has a state entry
            if (!playerStates.ContainsKey(userId))
            {
                playerStates[userId] = new PlayerState();
            }

            // Check for commands
            if (message.StartsWith("/"))
            {
                string command = message.Substring(1).ToLower();

                if (command == "erase")
                {
                    playerStates[userId].IsErasing = true;
                    client.Say($"Erasing mode activated for {args.Avatar.User.Name}");
                }
                else if (command == "build")
                {
                    playerStates[userId].IsErasing = false;
                    client.Say($"Building mode activated for {args.Avatar.User.Name}");
                }
                else if (command == "help")
                {
                    client.Say("Available commands: /erase, /build, /help, /listtextures");
                    client.Say("To select a texture, type /texture_name (e.g., /brick)");
                }
                else if (command == "listtextures")
                {
                    string textures = string.Join(", ", textureMapping.Keys);
                    client.Say($"Available textures: {textures}");
                }
                else
                {
                    // Assume it's a texture command
                    if (textureMapping.TryGetValue(command, out string actualTexture))
                    {
                        playerStates[userId].SelectedTexture = actualTexture;
                        playerStates[userId].IsErasing = false; // Ensure we're in building mode
                        client.Say($"{args.Avatar.User.Name} selected texture: {command}");
                    }
                    else
                    {
                        client.Say($"Unknown texture '{command}'. Type /listtextures to see available textures.");
                    }
                }
            }

        }

        private void Client_ObjectDeleted(VirtualParadiseClient sender, ObjectDeleteArgs args)
        {
            if (args.Object.Description == "inplay")
            {
                Vector3 snappedPosition = new Vector3(
                    SnapToGrid(args.Object.Position.X),
                    SnapToGrid(args.Object.Position.Y),
                    SnapToGrid(args.Object.Position.Z)
                );
                occupiedPositions.Remove(snappedPosition);
            }
        }

        private void Client_ObjectCreated(VirtualParadiseClient sender, ObjectCreateArgs args)
        {
            if (args.Object.Description == "inplay")
            {
                Vector3 snappedPosition = new Vector3(
                    SnapToGrid(args.Object.Position.X),
                    SnapToGrid(args.Object.Position.Y),
                    SnapToGrid(args.Object.Position.Z)
                );
                occupiedPositions.Add(snappedPosition);
            }
        }

        private async void Client_ObjectClicked(VirtualParadiseClient sender, ObjectClickArgs args)
        {
            var position = args.HitPoint;
            int userId = args.Avatar.User.Id;

            // Ensure the player has a state entry
            if (!playerStates.ContainsKey(userId))
            {
                playerStates[userId] = new PlayerState();
            }

            var playerState = playerStates[userId];

            if (playerState.IsErasing)
            {
                // Erase mode: Remove the cube if it's one of ours
                if (args.Object != null)
                {
                    var obj = await client.GetObjectAsync(args.Object.Id);

                    if (obj.Description == "inplay")
                    {
                        // Remove the object
                        await client.DeleteObjectAsync(obj.Id);

                        // Update occupied positions
                        Vector3 snappedPosition = new Vector3(
                            SnapToGrid(obj.Position.X),
                            SnapToGrid(obj.Position.Y),
                            SnapToGrid(obj.Position.Z)
                        );
                        occupiedPositions.Remove(snappedPosition);
                    }
                }
            }
            else
            {
                // Building mode
                if (args.Object == null)
                {
                    // Handle clicks on the ground
                    Vector3 placePos = new Vector3(
                        SnapToGrid(position.X),
                        SnapToGrid(position.Y),
                        SnapToGrid(position.Z)
                    );

                    if (!IsPositionOccupied(placePos))
                    {
                        VpObject objToPlace = new VpObject
                        {
                            Position = placePos,
                            Owner = args.Avatar.User.Id,
                            Model = "p2cube0100",
                            Description = "inplay",
                            Action = string.IsNullOrEmpty(playerState.SelectedTexture) ? "" : $"create texture {playerState.SelectedTexture}"
                        };
                        await client.AddObjectAsync(objToPlace);

                        // Update occupied positions
                        occupiedPositions.Add(placePos);
                    }
                }
                else
                {
                    var obj = await client.GetObjectAsync(args.Object.Id);

                    if (obj.Description == "inplay")
                    {
                        // Adjust cube center considering the origin at the base
                        double cubeCenterY = obj.Position.Y + (cellSize / 2);
                        Vector3 cubeCenter = new Vector3(obj.Position.X, cubeCenterY, obj.Position.Z);

                        // Calculate the vector from the cube's center to the hit point
                        Vector3 hitVector = new Vector3(
                            position.X - cubeCenter.X,
                            position.Y - cubeCenterY,
                            position.Z - cubeCenter.Z
                        );

                        // Normalize the hit vector
                        double length = Math.Sqrt(
                            hitVector.X * hitVector.X +
                            hitVector.Y * hitVector.Y +
                            hitVector.Z * hitVector.Z
                        );
                        if (length > 0)
                        {
                            hitVector.X /= length;
                            hitVector.Y /= length;
                            hitVector.Z /= length;
                        }

                        // Define the normal vectors for each face of the cube
                        Vector3[] faceNormals = new Vector3[]
                        {
                    new Vector3(-1, 0, 0), // Left face (-X)
                    new Vector3(1, 0, 0),  // Right face (+X)
                    new Vector3(0, -1, 0), // Bottom face (-Y)
                    new Vector3(0, 1, 0),  // Top face (+Y)
                    new Vector3(0, 0, -1), // Back face (-Z)
                    new Vector3(0, 0, 1)   // Front face (+Z)
                        };

                        Vector3[] faceOffsets = new Vector3[]
                        {
                    new Vector3(-cellSize, 0, 0), // Left face (-X)
                    new Vector3(cellSize, 0, 0),  // Right face (+X)
                    new Vector3(0, -cellSize, 0), // Bottom face (-Y)
                    new Vector3(0, cellSize, 0),  // Top face (+Y)
                    new Vector3(0, 0, -cellSize), // Back face (-Z)
                    new Vector3(0, 0, cellSize)   // Front face (+Z)
                        };

                        // Find the face with the maximum dot product
                        double maxDot = double.MinValue;
                        int maxIndex = -1;

                        for (int i = 0; i < faceNormals.Length; i++)
                        {
                            double dot = hitVector.X * faceNormals[i].X +
                                         hitVector.Y * faceNormals[i].Y +
                                         hitVector.Z * faceNormals[i].Z;
                            if (dot > maxDot)
                            {
                                maxDot = dot;
                                maxIndex = i;
                            }
                        }

                        // Use the offset corresponding to the face with the maximum dot product
                        Vector3 offset = faceOffsets[maxIndex];

                        // Compute the new cube position
                        Vector3 newPosition = obj.Position + offset;

                        // Snap the new position to the grid
                        Vector3 placePos = new Vector3(
                            SnapToGrid(newPosition.X),
                            SnapToGrid(newPosition.Y),
                            SnapToGrid(newPosition.Z)
                        );

                        // Check if position is occupied
                        if (!IsPositionOccupied(placePos))
                        {
                            VpObject objToPlace = new VpObject
                            {
                                Position = placePos,
                                Owner = args.Avatar.User.Id,
                                Model = "p2cube0100",
                                Description = "inplay",
                                Action = string.IsNullOrEmpty(playerState.SelectedTexture) ? "" : $"create texture {playerState.SelectedTexture}"
                            };
                            await client.AddObjectAsync(objToPlace);

                            // Update occupied positions
                            occupiedPositions.Add(placePos);
                        }
                        else
                        {
                            client.Say("Position is occupied.");
                        }
                    }
                    else
                    {
                        // Do not place a cube when clicking on other objects
                    }
                }
            }
        }

        public async Task Connect(string username, string password, string botName, string worldName, double x, double y, double z)
        {
            World world = new World { Name = worldName };
            client.Configuration = new VirtualParadiseClientConfiguration
            {
                ApplicationName = "Tomsbot",
                ApplicationVersion = "0.0.1",
                BotName = botName,
                UserName = username,
                World = world
            };

            try
            {
                await client.LoginAndEnterAsync(password, true);
            }
            catch (Exception ex)
            {
                throw new Exception("Login failed: " + ex.Message);
            }

            client.UpdateAvatar(x, y, z);

            // Initialize occupied positions around the bot's position
            await InitializeOccupiedPositions(x, z, 5);
        }

        private double SnapToGrid(double coordinate)
        {
            return Math.Round(coordinate / cellSize) * cellSize;
        }

        private async Task InitializeOccupiedPositions(double centerX, double centerZ, int radiusInCells)
        {
            for (int offsetX = -radiusInCells; offsetX <= radiusInCells; offsetX++)
            {
                for (int offsetZ = -radiusInCells; offsetZ <= radiusInCells; offsetZ++)
                {
                    int cellX = (int)Math.Floor(centerX / 10) + offsetX;
                    int cellZ = (int)Math.Floor(centerZ / 10) + offsetZ;

                    QueryCellResult result = await client.QueryCellAsync(cellX, cellZ);

                    foreach (VpObject vpObj in result.Objects)
                    {
                        if (vpObj.Description == "inplay")
                        {
                            Vector3 snappedPosition = new Vector3(
                                SnapToGrid(vpObj.Position.X),
                                SnapToGrid(vpObj.Position.Y),
                                SnapToGrid(vpObj.Position.Z)
                            );
                            occupiedPositions.Add(snappedPosition);
                        }
                    }
                }
            }
        }

        private bool IsPositionOccupied(Vector3 position)
        {
            bool occupied = occupiedPositions.Contains(position);
            if (occupied)
            {
                Console.WriteLine($"Position {position} is occupied.");
            }
            return occupied;
        }
    }

    // Custom comparer for Vector3
    public class Vector3Comparer : IEqualityComparer<Vector3>
    {
        private readonly double tolerance;

        public Vector3Comparer(double tolerance)
        {
            this.tolerance = tolerance;
        }

        public bool Equals(Vector3 a, Vector3 b)
        {
            return Math.Abs(a.X - b.X) <= tolerance &&
                   Math.Abs(a.Y - b.Y) <= tolerance &&
                   Math.Abs(a.Z - b.Z) <= tolerance;
        }

        public int GetHashCode(Vector3 obj)
        {
            int xHash = (int)(Math.Round(obj.X / tolerance));
            int yHash = (int)(Math.Round(obj.Y / tolerance));
            int zHash = (int)(Math.Round(obj.Z / tolerance));
            return xHash ^ yHash ^ zHash;
        }
    }
}
