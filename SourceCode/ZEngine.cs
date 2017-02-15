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
    
    public Direction[]  CARDINALS = {Direction.North, Direction.East, Direction.South, Direction.West};
    
    private ushort      mBotID;
    private Map         mMap;
    private List<Site>  mMySites = new List<Site>();
    private int         mRound = 0;
    
    public void Run() {
        mMap = Networking.getInit(out mBotID);
        
        InitSiteNeighbors();
        CalculateExpansionValue();
        
        Networking.SendInit(BOT_NAME);
        
        while (true) {
            string line = Console.ReadLine();
            if (line == null) break;

            mMap.Update(line);
            
            PrepareSites();
            CalculateMaps();
            DoMoves();
            ReviewMoves();
            
            Networking.SendMoves(mMySites);
            
            mRound++;
        }
    }

    private void InitSiteNeighbors() {
        for (ushort y = 0; y < mMap.Height; y++) {
            for (ushort x = 0; x < mMap.Width; x++) {
                var site = mMap[x, y];
                foreach (Direction d in CARDINALS) {
                    var xn = x;
                    var yn = y;
                    if (d == Direction.West)  {xn = (ushort)(xn == 0 ? mMap.Width - 1 : xn - 1);}
                    if (d == Direction.East)  {xn = (ushort)(xn == mMap.Width - 1 ? 0 : xn + 1);}
                    if (d == Direction.North) {yn = (ushort)(yn == 0 ? mMap.Height - 1 : yn - 1);}
                    if (d == Direction.South) {yn = (ushort)(yn == mMap.Height - 1 ? 0 : yn + 1);}
                    var ns = mMap[xn, yn];
                    site.Neighbors.Add(d, ns);
                }
            }
        }
    }

    private void CalculateExpansionValue() {
        var max = Math.Min(mMap.Width, mMap.Height) / 3;
        for (ushort y = 0; y < mMap.Height; y++) {
            for (ushort x = 0; x < mMap.Width; x++) {
                var site = mMap[x, y];
                site.ExpansionValue = 0;
                foreach (var d1 in site.Neighbors.Keys) {
                    var ns1 = site;
                    for (int i = 0; i < max; i++) {
                        ns1 = ns1.Neighbors[d1];
                        site.ExpansionValue += (double)ns1.Production / Math.Max(1.0, ns1.Strength);
                        var d2 = TurnRight(d1);
                        var ns2 = ns1;
                        for (int j = 0; j < max; j++) {
                            ns2 = ns2.Neighbors[d2];
                            site.ExpansionValue += (double)ns2.Production / Math.Max(1.0, ns2.Strength);
                        }
                    }
                }
            }
        }
    }

    private void PrepareSites() {
        mMySites.Clear();
        for (ushort y = 0; y < mMap.Height; y++) {
            for (ushort x = 0; x < mMap.Width; x++) {
                var site = mMap[x, y];
                site.InitRound();
                if (site.Owner != mBotID) continue;
                mMySites.Add(site);
                site.EnemyValue = CalcEnemyValue(site);
                if (site.EnemyValue == 0) site.BorderValue = CalcBorderValue(site);
            }
        }
    }

    private double CalcBorderValue(Site site) {
        double max = 0;
        foreach (var ns1 in site.Neighbors.Values) {
            if (ns1.Owner == 0 && ns1.ExpansionValue > max) max = ns1.ExpansionValue;
        }
        return max;
        
    }
    
    private int CalcEnemyValue(Site site) {
        int sum = 0;
        foreach (var ns1 in site.Neighbors.Values) {
            foreach (var ns2 in ns1.Neighbors.Values) {
                if (IsEnemySite(ns2)) sum += ns2.Strength;
                foreach (var ns3 in ns2.Neighbors.Values) {
                    if (IsEnemySite(ns3)) sum += ns3.Strength;
                }
            }
        }
        return sum * 100;
    }
    
    private void CalculateMaps() {
        int max = Math.Min(mMap.Width, mMap.Height) / 5;
        foreach (var site in mMySites.Where(s => s.EnemyValue != 0)) {
            BreadthFirstSearch(site, site.EnemyValue, Math.Max(6, max), true);
        }
        foreach (var site in mMySites.Where(s => s.BorderValue != 0)) {
            BreadthFirstSearch(site, site.BorderValue, 4, false);
        }
    }
    
    private void BreadthFirstSearch(Site site, double site_value, int max_dist, bool near_enemy) {
        var queue = new Queue<Site>();
        var done = new List<Site>();
        
        queue.Enqueue(site);
        site.MoveValue += site_value;
        site.Distance = 1;
        site.NearEnemy = near_enemy;
        done.Add(site);
            
        while(queue.Count != 0) {
            var current = queue.Dequeue();
            foreach (Site ns in current.Neighbors.Values) {
                var dist = current.Distance + 1;
                if (dist > max_dist) continue;
                if (done.Contains(ns)) continue;
                ns.MoveValue += site_value / (double)(dist * dist);
                ns.Distance = dist;
                if (ns.NearEnemy == false)
                    ns.NearEnemy = near_enemy;
                queue.Enqueue(ns);
                done.Add(ns);
            }
        }
    }
    
    private void DoMoves() {
        foreach (var my_site in mMySites.Where(s => s.EnemyValue != 0).OrderByDescending(s => s.EnemyValue)) {
            if (my_site.Strength < my_site.Production * 5) {
                my_site.SetMove(Direction.Still);
                continue;
            }
            Direction best_direction = GetBestDirectionToGo(my_site);
            if (best_direction == Direction.Still) {
                my_site.SetMove(Direction.Still);
                continue;
            }
            var target_site = my_site.Neighbors[best_direction];
            if (my_site.Strength > target_site.Strength || (my_site.Strength > 0 && target_site.Strength == 255)) {
                my_site.SetMove(best_direction);
                continue;
            }
        }
        var unmoved = new List<Site>();
        foreach (var my_site in mMySites.Where(s => s.BorderValue != 0).OrderByDescending(s => s.BorderValue)) {
            if (my_site.Strength < my_site.Production * 4) {
                my_site.SetMove(Direction.Still);
                continue;
            }
            if (my_site.NearEnemy) {
                my_site.BorderValue = 0;
                continue;
            }
            Direction best_direction = GetBestDirectionToGo(my_site);
            if (best_direction == Direction.Still) {
                my_site.SetMove(best_direction);
                continue;
            }
            var target_site = my_site.Neighbors[best_direction];
            if (target_site.Owner == 0 && my_site.Strength > target_site.Strength) {
                my_site.SetMove(best_direction);
                continue;
            }
            my_site.SetMove(Direction.Still);
            my_site.TargetDirection = best_direction;
            if (target_site.Strength != 0) {
                my_site.TargetValue = (double)target_site.Production / (double)target_site.Strength;
                unmoved.Add(my_site);
            }
        }
        foreach (var my_site in unmoved.OrderByDescending(s => s.TargetValue)) {
            if (my_site.HasMoved && my_site.Move != Direction.Still) continue;
            var target_site = my_site.Neighbors[my_site.TargetDirection];
            foreach (var help_direction in my_site.Neighbors.Keys) {
                var help_site = my_site.Neighbors[help_direction];
                if (help_site.Owner != mBotID) continue;
                if (help_site.NearEnemy) continue;
                if (help_site.Strength < help_site.Production * 4) continue;
                if (help_site.HasMoved && help_site.Move != Direction.Still) continue;
                if (help_site.Strength + my_site.Strength > target_site.Strength) {
                    help_site.SetMove(Opposite(help_direction));
                    break;
                }
            }
        }
        foreach (var my_site in mMySites.Where(s => s.EnemyValue == 0 && s.BorderValue == 0)) {
            if (my_site.HasMoved) continue;
            if (my_site.Strength < my_site.Production * 5) {
                my_site.SetMove(Direction.Still);
                continue;
            }
            double best_value = 0;
            Direction best_direction = Direction.Still;
            foreach (var d in my_site.Neighbors.Keys) {
                var ns = my_site.Neighbors[d];
                if (ns.Owner != mBotID) continue;
                if (my_site.NearEnemy == true && ns.Owner != 0 && ns.MoveValue > best_value) {
                    best_value = ns.MoveValue;
                    best_direction = d;
                }
                if (my_site.NearEnemy == false && ns.MoveValue > best_value) {
                    best_value = ns.MoveValue;
                    best_direction = d;
                }
            }
            if (best_direction != Direction.Still || my_site.NearEnemy) {
                my_site.SetMove(best_direction);
                continue;
            }
            my_site.SetMove(GetExpandDirection(my_site));
        }
    }

    public Direction GetBestDirectionToGo(Site site) {
        double      best_value = -1;
        Direction   best_direction = Direction.Still;

        foreach (Direction d in site.Neighbors.Keys) {
            var ns = site.Neighbors[d];
            if (ns.Owner == mBotID) continue;
            double site_value = 0;
            site_value += (double)ns.Production / Math.Max(1.0, ns.Strength);
            site_value += site.Strength * ns.Neighbors.Values.Where(s => IsEnemySite(s)).Count();
            if (site_value > best_value) {
                best_direction = d;
                best_value = site_value;
            }
        }
        
        return best_direction;
    }
    
    private Direction GetExpandDirection(Site site) {
        var direction = Direction.North;
        var max_dist = Math.Min(mMap.Width, mMap.Height) / 2;
        var best_dist = max_dist;
        double best_value = 0;

        foreach (Direction d in site.Neighbors.Keys) {
            var goal_site = site.Neighbors[d];
            var distance = 0;
            
            while (goal_site.MoveValue == 0 && distance < max_dist) {
                distance++;
                goal_site = goal_site.Neighbors[d];
            }
            
            if (distance < best_dist || (distance == best_dist && goal_site.MoveValue > best_value)) {
                direction = d;
                best_dist = distance;
                best_value = goal_site.MoveValue;
            }
        }

        return direction;
    }
    
    private void ReviewMoves() {
        var moving = mMySites.Where(s => s.Move != Direction.Still);
        foreach (var my_site in moving) {
            var ns = my_site.Neighbors[my_site.Move];
            if (ns.AddedStrenght > 275)
                my_site.SetMove(Direction.Still);
        }
    }    

    private Direction TurnRight(Direction d) {
        if (d == Direction.North) return Direction.East;
        if (d == Direction.East)  return Direction.South;
        if (d == Direction.South) return Direction.West;
        if (d == Direction.West)  return Direction.North;
        return Direction.Still;
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
}
