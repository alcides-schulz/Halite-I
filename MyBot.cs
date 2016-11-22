using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MyBot
{
    public const string MyBotName = "zluhcsOld";
	
	public static ushort MyID;
	private static Random random = new Random();
	
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
		Site s = map[x, y];
		
		foreach (Direction d in Enum.GetValues(typeof(Direction))) {
			if (d == Direction.Still) continue;
			Site n = GetNeighbour(map, move.Location, d);
			// Log.Information("n (" + x + "," + y + ") d=" + d + " n.str=" + n.Strength + " s.str=" + s.Strength);
			if (n.Owner != MyID && n.Strength < s.Strength) {
				move.Direction = d;
				return move;
			}
		}
		
		if (s.Strength < s.Production * 5) {
			move.Direction = Direction.Still;
		}
		else {
			if (random.Next(10) > 5)
				move.Direction = Direction.North;
			else
				move.Direction = Direction.West;
		}
			
		return move;
	}
	
    public static void Main(string[] args) {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

		//Log.Setup("log.txt");
		
        var map = Networking.getInit(out MyID);

        /* ------
            Do more prep work, see rules for time limit
        ------ */

        Networking.SendInit(MyBotName); // Acknoweldge the init and begin the game

        //var random = new Random();
		int round = 0;
        while (true) {
            Networking.getFrame(ref map); // Update the map to reflect the moves before this turn

			round += 1;
			
			//PrintBoard(map, round);
            var moves = new List<Move>();
            for (ushort x = 0; x < map.Width; x++) {
                for (ushort y = 0; y < map.Height; y++) {
                    if (map[x, y].Owner == MyID) {
						moves.Add(GetMove(map, x, y));
                        // moves.Add(new Move {
                            // Location = new Location {X = x, Y = y},
                            // Direction = (Direction)random.Next(5)
                        // });
                    }
                }
            }

            Networking.SendMoves(moves); // Send moves
        }
    }
	
	private static void PrintBoard(Map map, int round) {
		string name = "Round-" + round + ".csv";
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
            File.AppendAllLines(name, new[] {line});
		}		
	}
	
	
}
