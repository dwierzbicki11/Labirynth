#!/bin/bash
# 1. Synchronizacja zmian z laptopa na RPi (tylko kod źródłowy)
rsync -avz --exclude 'bin' --exclude 'obj' ~/Labirynth zonderq@rpi-dev:~/Labirynth/

# 2. Zdalne wyzwalanie kompilacji na RPi (z użyciem .NET SDK 11)
ssh rpi-dev "cd ~/Labirynth && dotnet build -c Release"

# 3. Jeśli kompilacja przeszła, pobierz wynik (opcjonalne)
rsync -avz zonderq@rpi-dev:~/Labirynth/bin/Release/ ~/Labirynth/bin/Release/

echo "Build zakończony."
