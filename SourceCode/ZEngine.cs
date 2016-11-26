using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

//
// Zluhcs Engine for Halite AI Programming
//
public class ZEngine
{
    public const string BOT_NAME = "zluhcs";
	
    private ushort 		mBotID;
    private Map         mMap;

    private Direction[] CARDINALS = {Direction.North, Direction.East, Direction.South, Direction.West};

    public ZEngine() {

    }

    public void Run() {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);

        Log.Setup("prodx.txt");

        mMap = Networking.getInit(out mBotID);
		
        Networking.SendInit(BOT_NAME);

        while (true) {
			
            Networking.getFrame(ref mMap);

            var moves = new List<Move>();

            for (ushort y = 0; y < mMap.Height; y++) {
                for (ushort x = 0; x < mMap.Width; x++) {
                    if (mMap[x, y].Owner == mBotID) {
					    moves.Add(GetMove(mMap, x, y));
                    }
                }
            }

            Networking.SendMoves(moves);
        }
    }

	public Move GetMove(Map map, ushort x, ushort y) {
		Move move = new Move();

		move.Location.X = x;
		move.Location.Y = y;
		
		Site my_site = map[x, y];

        Direction best_direction = GetBestDirectionToGo(move.Location);

        if (best_direction != Direction.Still) {
			var best_location = GetNeighbour(mMap, move.Location, best_direction);
            var best_site = mMap[best_location.X, best_location.Y];
            if (best_site.Strength < my_site.Strength) {
                move.Direction = best_direction;
                return move;
            }
        }
		
		if (my_site.Strength < my_site.Production * 5) {
			move.Direction = Direction.Still;
			return move;
		}
		
        if (!IsBorder(move.Location)) {
            move.Direction = FindNearestEnemyDirection(move.Location);
            return move;
        }

        move.Direction = Direction.Still;
		return move;
	}

    public Direction GetBestDirectionToGo(Location location) {
        double      best_value = -1.0;
        Direction   best_direction = Direction.Still;

        foreach (Direction d in CARDINALS) {
            var nl = GetNeighbour(mMap, location, d);
            var ns = mMap[nl.X, nl.Y];
            
            if (ns.Owner == mBotID) continue;

            double site_value = 0;
            if (ns.Owner == 0 && ns.Strength > 0) {
                double site_strength = ns.Strength;
                double site_production = ns.Production;
                site_value = site_production / site_strength;
            }
            else {
                site_value = GetTotalDamage(nl);
            }

            if (site_value > best_value) {
                best_direction = d;
                best_value = site_value;
            }
        }

        return best_direction;
    }

    private double GetTotalDamage(Location location) {
        double total_damage = 0;
        foreach (Direction d in CARDINALS) {
            var nl = GetNeighbour(mMap, location, d);
            var ns = mMap[nl.X, nl.Y];
            if (ns.Owner != 0 && ns.Owner != mBotID) 
                total_damage += ns.Strength;
        }
        return total_damage;
    }

    private bool IsBorder(Location location) {
		foreach (Direction d in CARDINALS) {
			var nl = GetNeighbour(mMap, location, d);
			var ns = mMap[nl.X, nl.Y];
			if (ns.Owner != mBotID)
                return true;
		}
        return false;
    }

    private Direction FindNearestEnemyDirection(Location location) {
        var direction = Direction.North;
        var max_dist = Math.Min(mMap.Width, mMap.Height) / 2;

		foreach (Direction d in CARDINALS) {
			var l = GetNeighbour(mMap, location, d);
			var ns = mMap[l.X, l.Y];
            var distance = 0;
            var current = l;
			while (ns.Owner == mBotID && distance < max_dist) {
                distance++;
                current = GetNeighbour(mMap, current, d);
                ns = mMap[current.X, current.Y];
            }

            if (distance < max_dist) {
                direction = d;
                max_dist = distance;
            }
        }

        return direction;
    }

	public Location GetNeighbour(Map map, Location loc, Direction d) {
		ushort 	x = loc.X;
		ushort 	y = loc.Y;
		
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
