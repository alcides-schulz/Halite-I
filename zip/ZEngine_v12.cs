using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading;

//
// Zluhcs Engine for Halite AI Programming
//
public class ZEngine
{
    public const string BOT_NAME = "zluhcs";

    private ushort      mBotID;
    private Map         mMap;

	public Direction[]  CARDINALS = {Direction.North, Direction.East, Direction.South, Direction.West};
    
    private bool[,]     mMoved;
    private int         mRound = 0;
    private bool        mIsAttacking = false;
    private int[,]      mAttack;
    private int         mProductionLimit = 5;
    
    public Map          Map     {get {return mMap;}}
    public ushort       BotID   {get {return mBotID;}}
    
    public void Run() {
        Console.SetIn(Console.In);
        Console.SetOut(Console.Out);
		
        mMap = Networking.getInit(out mBotID);
        
        mMoved = new bool[mMap.Width, mMap.Height];
        mAttack = new int[mMap.Width, mMap.Height];
        
        //mProductionLimit -= Math.Min(mMap.Width, mMap.Height) / 10;

        Networking.SendInit(BOT_NAME);
		
		while (true) {
            string line = Console.ReadLine();
            if (line == null) break;

			mMap.Update(line);
            
            var moves = new List<Move>();
            
            for (ushort y = 0; y < mMap.Height; y++) {
                for (ushort x = 0; x < mMap.Width; x++) {
                    mMoved[x, y] = false;
                }
            }
            
            ExecuteNearBorderMove(mRound, moves);
            
            CalculateMoves(moves);
            
            Networking.SendMoves(moves);
            
            VerifyAttackEnd();
            
            mRound++;
        }
    }
    
    // Calculate moves near border, exclude combat moves.
    public void ExecuteNearBorderMove(int round, List<Move> moves) {
        //if (mIsAttacking) return;
        
        for (ushort y = 0; y < mMap.Height; y++) {
            for (ushort x = 0; x < mMap.Width; x++) {
                
                if (mMoved[x, y]) continue;
                
                var my_site = mMap[x, y];
                
                if (my_site.Owner != mBotID) continue;
                if (my_site.Strength < my_site.Production * 5) continue;
                if (mAttack[my_site.X, my_site.Y] != 0) continue;
                
                Direction best_border_direction = Direction.Still;
                double best_border_value = 0;
                bool is_combat_site = false;
                
                foreach (Direction d in CARDINALS) {
                    var ns = GetSite(my_site.X, my_site.Y, d);
                    if (IsCombatSite(ns)) {
                        is_combat_site = true;
                        break;
                    }
                    if (ns.Owner == 0 && ns.Strength != 0) {
                        double value = (double)ns.Production / (double)ns.Strength;
                        if (value > best_border_value) {
                            best_border_value = value;
                            best_border_direction = d;
                        }
                    }
                }
                
                // not border or combat site
                if (best_border_direction == Direction.Still || is_combat_site) continue;
                
                // Direct move to weakest site
                var border_site = GetSite(my_site.X, my_site.Y, best_border_direction);
                if (my_site.Strength > border_site.Strength) {
                    continue;
                }
                
                // Check if help from another site can expand territory
                foreach (Direction d in CARDINALS) {
                    var help_site = GetSite(my_site.X, my_site.Y, d);
                    if (help_site.Owner != mBotID) continue;
                    if (mMoved[help_site.X, help_site.Y]) continue;
                    if (CanWinTerritory(help_site)) continue;
                    if (Math.Min(255, help_site.Strength + my_site.Strength) > border_site.Strength) {
                        moves.Add(new Move(help_site.X, help_site.Y, Opposite(d)));
                        mMoved[help_site.X, help_site.Y] = true;
                        break;
                    }
                }
            }
        }
    }
    
    private bool CanWinTerritory(Site site) {
        foreach (Direction d in CARDINALS) {
            var ns = GetSite(site.X, site.Y, d);
            if (ns.Owner == 0 && ns.Strength < site.Strength) return true;
        }
        return false;
    }
    
    public bool IsCombatSite(Site site) {
        foreach (Direction d in CARDINALS) {
            var ns = GetSite(site.X, site.Y, d);
            if (ns.Owner != 0 && ns.Owner != mBotID) return true;
        }
        return false;
    }
    
    public void CalculateMoves(List<Move> moves) {
        for (ushort y = 0; y < mMap.Height; y++) {
            
            for (ushort x = 0; x < mMap.Width; x++) {
                
                if (mMap[x, y].Owner != mBotID || mMoved[x, y])
                    continue;
    
                mMoved[x, y] = true;

                Site my_site = mMap[x, y];

                Direction best_direction = GetBestDirectionToGo(x, y);
                if (best_direction != Direction.Still) {
                    var target_site = GetSite(x, y, best_direction);

                    if (mIsAttacking == false) {
                        foreach (Direction d in CARDINALS) {
                            var enemy = GetSite(target_site.X, target_site.Y, d);
                            if (enemy.Owner != 0 && enemy.Owner != mBotID) {
                                CalculateAttack(my_site, target_site, enemy);
                                break;
                            }
                        }
                    }
                    
                    if (target_site.Strength < my_site.Strength) {
                        moves.Add(new Move(x, y, best_direction));
                        continue;
                    }
                }

                if (my_site.Strength < my_site.Production * mProductionLimit) {
                    moves.Add(new Move(x, y, Direction.Still));
                    continue;
                }

                if (mIsAttacking) {
                    var attack_direction = Direction.Still;
                    foreach (Direction d in CARDINALS) {
                        var next = GetSite(my_site.X, my_site.Y, d);
                        if (mAttack[next.X, next.Y] != 0 && mAttack[next.X, next.Y] > mAttack[my_site.X, my_site.Y])
                            attack_direction = d;
                    }
                    if (attack_direction != Direction.Still) {
                        moves.Add(new Move(x, y, attack_direction));
                        continue;
                    }
                }
                
                if (!IsBorder(x, y)) {
                    var direction = FindNearestGoalDirection(x, y);
                    var ns = GetSite(x, y, direction);
                    if (my_site.Strength > 200 || ns.Strength + my_site.Strength < 255) {
                        moves.Add(new Move(x, y, direction));
                        continue;
                    }
                    moves.Add(new Move(x, y, GetBestStrenghtDirection(x, y)));
                    continue;
                }

                moves.Add(new Move(x, y, Direction.Still));
            }
        }
    }

    private Direction GetBestStrenghtDirection(ushort x, ushort y) {
        int         current_strenght = mMap[x, y].Strength;
        int         best_strength = -1000;
        Direction   best_direction = Direction.Still;
        
        foreach (Direction d in CARDINALS) {
            var ns = GetSite(x, y, d);
            int combine = 255 - (current_strenght + ns.Strength);
            if (combine > best_strength) {
                best_strength = combine;
                best_direction = d;
            }
        }
        
        return best_direction;
    }
    
    public Direction GetBestDirectionToGo(ushort x, ushort y) {
        double      best_value = -1.0;
        Direction   best_direction = Direction.Still;
        double      my_strenght = mMap[x, y].Strength;

        foreach (Direction d in CARDINALS) {
            var ns = GetSite(x, y, d);
            if (ns.Owner == mBotID) continue;

            double site_value = 0;
            if (ns.Owner == 0) {
                if (ns.Strength != 0)
                    site_value = (double)ns.Production / (double)ns.Strength;
                else
                    site_value = (double)ns.Production;
            }
            site_value += my_strenght * EnemyCount(ns.X, ns.Y);

            if (site_value > best_value) {
                best_direction = d;
                best_value = site_value;
            }
        }

        return best_direction;
    }
    
    private int EnemyCount(ushort x, ushort y) {
        int     count = 0;
        
        foreach (Direction d in CARDINALS) {
            var site = GetSite(x, y, d);
            if (site.Owner != 0 && site.Owner != mBotID) 
                count += 1;
        }
        
        return count;
    }

    private double GetTotalDamage(ushort x, ushort y) {
        double total_damage = 0;
        foreach (Direction d in CARDINALS) {
            var site = GetSite(x, y, d);
            if (site.Owner != 0 && site.Owner != mBotID) 
                total_damage += site.Strength;
        }
        return total_damage;
    }

    public bool IsBorder(ushort x, ushort y) {
        foreach (Direction d in CARDINALS) {
            var site = GetSite(x, y, d);
            if (site.Owner != mBotID)
                return true;
        }
        return false;
    }
    
    public bool GetSiteMoved(ushort x, ushort y) {
        return mMoved[x, y];
    }

    public void SetSiteMoved(ushort x, ushort y, bool moved) {
        mMoved[x, y] = moved;
    }

    private Direction FindNearestGoalDirection(ushort x, ushort y) {
        var direction = Direction.North;
        var max_dist = Math.Min(mMap.Width, mMap.Height) / 2;
        double current_goal_value = 0;
        var best_dist = 999999;

        foreach (Direction d in CARDINALS) {
            var goal_site = GetSite(x, y, d);
            var distance = 0;
            
            while (goal_site.Owner == mBotID && distance < max_dist) {
                distance++;
                goal_site = GetSite(goal_site.X, goal_site.Y, d);
            }
            
            if (goal_site.Owner == mBotID) continue;
            
            var new_goal_value = GetGoalValue(goal_site, d);

            if (distance < best_dist || (distance <= best_dist + 2 && new_goal_value >= current_goal_value)) {
                direction = d;
                best_dist = distance;
                current_goal_value = new_goal_value;
            }
        }

        return direction;
    }
    
    private double GetGoalValue(Site goal_site, Direction d) {
        double value = 0;
        
        for (int i = 0; i < 4; i++) {
            value += GetSiteValue(goal_site);
            if (d == Direction.North || d == Direction.South) {
                value += GetGoalDirectionValue(goal_site, Direction.East);
                value += GetGoalDirectionValue(goal_site, Direction.West);
            }
            if (d == Direction.East || d == Direction.West) {
                value += GetGoalDirectionValue(goal_site, Direction.North);
                value += GetGoalDirectionValue(goal_site, Direction.South);
            }
            goal_site = GetSite(goal_site.X, goal_site.Y, d);
        }
        
        return value;
    }

    private double GetGoalDirectionValue(Site goal_site, Direction d) {
        double value = 0;
        
        for (int i = 0; i < 4; i++) {
            goal_site = GetSite(goal_site.X, goal_site.Y, d);
            value += GetSiteValue(goal_site);
        }
        
        return value;
    }
    
    private double GetSiteValue(Site site) {
        if (site.Owner == mBotID) return 0;
        
        if (site.Owner == 0 && site.Strength != 0) 
            return (double)site.Production / (double)site.Strength;

        //return GetTotalDamage(site.X, site.Y);
        return (double)site.Production;
    }

    public Site GetSite(ushort x, ushort y, Direction d) {
        if (d == Direction.West) {x = (ushort)((x == 0) ? mMap.Width - 1 : x - 1);}
        if (d == Direction.East) {x = (ushort)((x == mMap.Width - 1) ? 0 : x + 1);}
        if (d == Direction.North) {y = (ushort)((y == 0) ? mMap.Height - 1 : y - 1);}
        if (d == Direction.South) {y = (ushort)((y == mMap.Height - 1) ? 0 : y + 1);}
        return mMap[x, y];
    }

    public Site GetSite(Site site, Direction d) {
        return GetSite(site.X, site.Y, d);
    }
    
    public bool IsEnemySite(Site site) {
        return site.Owner != 0 && site.Owner != mBotID;
    }
    
    public Direction Opposite(Direction direction) {
        if (direction == Direction.North) return Direction.South;
        if (direction == Direction.South) return Direction.North;
        if (direction == Direction.East) return Direction.West;
        if (direction == Direction.West) return Direction.East;
        return Direction.Still;
    }

    private void CalculateAttack(Site my_site, Site connect, Site enemy) {
        
        if (mIsAttacking) return;
        
        for (ushort y = 0; y < mMap.Height; y++) {
		    for (ushort x = 0; x < mMap.Width; x++) {
				mAttack[x, y] = 0;
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
		
		var queue = new Queue<Site>();
		queue.Enqueue(connect);
		mAttack[connect.X, connect.Y] = 1;
		
		while(queue.Count != 0) {
			var current = queue.Dequeue();
			foreach (Direction d in CARDINALS) {
				Site n = GetSite(current.X, current.Y, d);
				if (mAttack[n.X, n.Y] == 0 && (n.Owner == mBotID || n.Owner == enemy.Owner)) {
					mAttack[n.X, n.Y] = mAttack[current.X, current.Y] + 1;
                    queue.Enqueue(n);
				}
			}
		}

        var max = Math.Min(mMap.Width, mMap.Height) / 3;

        for (ushort y = 0; y < mMap.Height; y++) {
		    for (ushort x = 0; x < mMap.Width; x++) {
                var site = mMap[x, y];
                if (site.Owner == mBotID) mAttack[x, y] *= -1;
                if (site.Owner == mBotID && mAttack[x, y] < -max) mAttack[x, y] = 0;
                if (site.Owner == enemy.Owner && mAttack[x, y] > max) mAttack[x, y] = 0;
			}
		}
        
        mIsAttacking = true;
        
        // string f = "attack-" + mRound + ".csv";
        // for (ushort y = 0; y < mMap.Height; y++) {
            // string line = "";
		    // for (ushort x = 0; x < mMap.Width; x++) {
				// if (line != "") line += ",";
                // line += mAttack[x, y];
			// }
            // File.AppendAllText(f, line + Environment.NewLine);
		// }
	}    

    private void VerifyAttackEnd() {
        if (mIsAttacking == false) return;
        
        int enemy_territory = 0;
        int my_count = 0;
        
        for (ushort y = 0; y < mMap.Height; y++) {
		    for (ushort x = 0; x < mMap.Width; x++) {
                if (mAttack[x, y] > 0) {
                    enemy_territory++;
                    var site = mMap[x, y];
                    if (site.Owner == mBotID) my_count++;
                }
			}
		}
        
        if (my_count >= enemy_territory - 10) mIsAttacking = false;
    }    
}
