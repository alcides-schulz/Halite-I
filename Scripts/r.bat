del *.csv
del debug\*.csv
del log.txt
rem set SEED=-s 3
halite %SEED% -q -d "30 30" -t "MyBot.exe" "MyBotOld.exe" 

