import os
from subprocess import check_output as qx
import math

GAMES_TO_PLAY = 1000
HALITE_RUNNER = "halite -q -t"
BOT_ONE = "MyBot.exe"
BOT_TWO = "MyBotOld.exe"
LOG_NAME = "batch_games.txt"
SEED = 0

total_score = 0.0

log_file = open(LOG_NAME, "w")

def run_game(game_number, seed, map_size, bot1, bot2):
    map_param = '-d "' + str(map_size) + " " + str(map_size) + '"'

    cmd = HALITE_RUNNER + " -s " + str(seed) + " " + map_param + " " + bot1 + " " + bot2
    
    print(" Game " + str(game_number) + "/" + str(GAMES_TO_PLAY) + ": " + cmd)
    log_file.write(cmd + '\n')
    
    output = qx(cmd)

    winner = ""
    for line in output.splitlines():
        line_string = line.decode()
        log_file.write(line_string + '\n')
        if line_string == "1 1":
           winner = bot1
        if line_string == "2 1":
           winner = bot2

    return winner


count1 = 0
count2 = 0
map_size = 20

for game_number in range(1, GAMES_TO_PLAY + 1):
    winner = ""

    if game_number > 1 and game_number % 2 == 1:
        map_size += 10
        if map_size > 40:
             map_size = 20

    if game_number % 2 == 1:
        SEED += 1
        winner = run_game(game_number, SEED, map_size, BOT_ONE, BOT_TWO)
    else:
        winner = run_game(game_number, SEED, map_size, BOT_TWO, BOT_ONE)

    if winner == BOT_ONE:
        count1 += 1
    if winner == BOT_TWO:
        count2 += 1
    win_fraction = float(count1) / float(game_number)
    win_pct = win_fraction * 100.0
    #ELO=-LOG(1/D16-1)*400/LOG(10)
    elo = 0.0
    if win_fraction > 0 and (1 / win_fraction - 1) > 0:
        elo = -math.log(1 / win_fraction - 1) * 400 / math.log(10)
    #LOS=0.5+0.5*ERF((A12-B12)/SQRT(2*A12+B12))
    los = 0.5 + 0.5 * math.erf((count1 - count2) / math.sqrt(2 * (count1 + count2)))
    los = los * 100
    
    result_line = "  -> winner: " + winner + " " + BOT_ONE + " " + str(count1) + " " + BOT_TWO + " " + str(count2) + " win_pct: " + str(win_pct) + " elo: " + str(elo) + " los: " + str(los)
    
    print(result_line)
    
    log_file.write(result_line + "\n")

    log_file.flush()

log_file.close()

