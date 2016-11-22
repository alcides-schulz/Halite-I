using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MyBot
{
    public const string 		mBotName = "zluhcs";
	public static ushort 		mBotID;
	
	private static int[,] 		mExpansionValue;
	private static Direction[,]	mExpansionDirection;
	// private static Random 		mRandom = new Random();
	 
	public static Site GetNeighbour(Map map, Location loc, Direction d) {
		ushort 	x = loc.X;
		ushort 	y = loc.Y;
		
		// Log.Information("loc: (" + x + "," + y + ") dir=" + d);
		
		if (d == Direction.West) {x = (ushort)((x == 0) ? map.Width - 1 : x - 1);}
		if (d == Direction.East) {x = (ushort)((x == map.Width - 1) ? 0 : x + 1);}
		if (d == Direction.North) {y = (ushort)((y == 0) ? map.Height - 1 : y - 1);}
		if (d == Direction.South) {y = (ushort)((y == map.Height - 1) ? 0 : y + 1);}
		
		return map[x, y];
	}
	
	public static Move GetMove(Map map, ushort x, ushort y) {
		Move move = new Move();
		
		move.Location.X = x;
		move.Location.Y = y;
		
		Site my_site = map[x, y];
		
		foreach (Direction d in Enum.GetValues(typeof(Direction))) {
			if (d == Direction.Still) continue;
			var l = GetNeighbourLocation(map, move.Location, d);
			var ns = map[l.X, l.Y];
			if (ns.Owner != mBotID && ns.Strength < my_site.Strength) {
				move.Direction = d;
				return move;
			}
		}
		
		if (my_site.Strength < my_site.Production * 5) {
			move.Direction = Direction.Still;
			return move;
		}
		
		// Expansion
		move.Direction = GetDirection(map, move.Location);
		
		// foreach (Direction d in Enum.GetValues(typeof(Direction))) {
			// if (d == Direction.Still) continue;
			// Location l = GetNeighbourLocation(map, move.Location, d);
			// var ns = map[l.X, l.Y];
			// if (mExpansionValue[x, y] < mExpansionValue[l.X, l.Y]) {
				// move.Direction = d;
				// return move;
			// }
		// }
		
		// if (mRandom.Next(10) > 5)
			// move.Direction = Direction.North;
		// else
			// move.Direction = Direction.West;
			
		return move;
	}
	
	private static Direction GetDirection(Map map, Location location) {
		var current_value = mExpansionValue[location.X, location.Y];
		var direction = mExpansionDirection[location.X, location.Y];
		
		for (int i = 0; i < 4; i++) {
			direction = NextDirection(direction);
			var n = GetNeighbourLocation(map, location, direction);
			var new_value = mExpansionValue[n.X, n.Y];
			if (new_value > current_value) {
				break;
			}
		}

		mExpansionDirection[location.X, location.Y] = direction;
		return direction;
	}
	
	private static Direction NextDirection(Direction d) {
		if (d == Direction.North) return Direction.East;
		if (d == Direction.East) return Direction.South;
		if (d == Direction.South) return Direction.West;
		if (d == Direction.West) return Direction.North;
		return Direction.Still;
	}
	
    public static void Main(string[] args) {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

		//Log.Setup("log.txt");
		
        var map = Networking.getInit(out mBotID);

		//PrintBoard(map, 0);
		CalculateExpansionValues(map);
		CalculateExpansionDirection(map);
		
        Networking.SendInit(mBotName); // Acknoweldge the init and begin the game

		int round = 0;
        while (true) {
			
            Networking.getFrame(ref map); // Update the map to reflect the moves before this turn

			round += 1;
			
			//PrintBoard(map, round);
            var moves = new List<Move>();
            for (ushort y = 0; y < map.Height; y++) {
                for (ushort x = 0; x < map.Width; x++) {
                    if (map[x, y].Owner == mBotID) {
						moves.Add(GetMove(map, x, y));
                    }
                }
            }

            Networking.SendMoves(moves); // Send moves
        }
    }
	
	private static void CalculateExpansionDirection(Map map) {
		mExpansionDirection = new Direction[map.Width, map.Height];
		
        for (ushort y = 0; y < map.Height; y++) {
		    for (ushort x = 0; x < map.Width; x++) {
				mExpansionDirection[x, y] = Direction.North;
			}
		}
	}
	
	private static void CalculateExpansionValues(Map map) {
		mExpansionValue = new int[map.Width, map.Height];
		
		Location starting = new Location();
        for (ushort y = 0; y < map.Height; y++) {
		    for (ushort x = 0; x < map.Width; x++) {
				if (map[x, y].Owner == mBotID) {
					starting.X = x;
					starting.Y = y;
				}
				mExpansionValue[x, y] = 0;
			}
		}

		// Breadth-First-Search(Graph, root):
			
			// for each node n in Graph:            
				// n.distance = INFINITY        
				// n.parent = NIL

			// create empty queue Q      

			// root.distance = 0
			// Q.enqueue(root)                      

			// while Q is not empty:        
				// current = Q.dequeue()
				// for each node n that is adjacent to current:
					// if n.distance == INFINITY:
						// n.distance = current.distance + 1
						// n.parent = current
						// Q.enqueue(n)		
		
		var queue = new Queue<Location>();
		queue.Enqueue(starting);
		mExpansionValue[starting.X, starting.Y] = 1;
		
		while(queue.Count != 0) {
			var current = queue.Dequeue();
			foreach (Direction d in Enum.GetValues(typeof(Direction))) {
				if (d == Direction.Still) continue;
				Location n = GetNeighbourLocation(map, current, d);
				if (mExpansionValue[n.X, n.Y] == 0) {
					mExpansionValue[n.X, n.Y] = mExpansionValue[current.X, current.Y] + 1;
					queue.Enqueue(n);
				}
			}
		}
		
		// PrintExpansionValues(map.Width, map.Height);
	}

	private static void PrintExpansionValues(ushort width, ushort height) {
		string filename = "ExpansionValues.csv";
		string line = "";
		string delim = "";
		
		for (ushort y = 0; y < height; y++) {
			line = "";
			delim = "";
			for (ushort x = 0; x < width; x++) {
				line += delim + mExpansionValue[x, y];
				delim = ",";
			}
            File.AppendAllLines(filename, new[] {line});
		}		
	}
	
	private static void PrintBoard(Map map, int round) {
		string filename = "Round-" + round + ".csv";
		string line = "";
		string delim = "";
		
		for (ushort y = 0; y < map.Height; y++) {
			line = "";
			delim = "";
			for (ushort x = 0; x < map.Width; x++) {
				Site s = map[x, y];
				line += delim + "o=" + s.Owner + " p=" + s.Production + " s=" + s.Strength;
				delim = ",";
			}
            File.AppendAllLines(filename, new[] {line});
		}		
	}
	
	public static Location GetNeighbourLocation(Map map, Location loc, Direction d) {
		ushort 	x = loc.X;
		ushort 	y = loc.Y;
		
		// Log.Information("loc: (" + x + "," + y + ") dir=" + d);
		
		if (d == Direction.West) {x = (ushort)((x == 0) ? map.Width - 1 : x - 1);}
		if (d == Direction.East) {x = (ushort)((x == map.Width - 1) ? 0 : x + 1);}
		if (d == Direction.North) {y = (ushort)((y == 0) ? map.Height - 1 : y - 1);}
		if (d == Direction.South) {y = (ushort)((y == map.Height - 1) ? 0 : y + 1);}
		
		var l = new Location();
		l.X = x;
		l.Y = y;
		
		return l;
	}
	
}
