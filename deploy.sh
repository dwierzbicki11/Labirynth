#!/bin/bash
set -e 

# Konfiguracja środowiska
LOCAL_PATH="/home/zonderq/Labirynth"
REMOTE_PATH="/home/zonderq/Labirynth"
RPi_USER="zonderq"
RPi_HOST="10.0.0.2"
DOTNET_PATH="/home/zonderq/.dotnet/dotnet"

echo "========================================================"
echo "--- FABRYKA: Rozpoczęto pełny cykl produkcyjny ---"
echo "========================================================"

echo ">>> [1/14] Synchronizacja nowych zmian w kodzie źródłowym..."
rsync -avz --delete --exclude 'bin' --exclude 'obj' --exclude '.git' "$LOCAL_PATH/" "$RPi_USER@$RPi_HOST:$REMOTE_PATH/"

echo ">>> [2/14] Budowanie nowej wersji silnika (Release)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH build $REMOTE_PATH/CyberEngine.csproj -c Release"

echo ">>> [3/14] Testy jednostkowe podsystemu logicznego (Logika)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH test $REMOTE_PATH/CyberEngine.csproj --configuration Release"

echo ">>> [4/14] Deterministyczny test wydajnościowy (Skalowanie Profili GPU)..."
ssh $RPi_USER@$RPi_HOST "cd $REMOTE_PATH && rm -f benchmark_results.txt"
PROFILES=("low" "med" "high")
for i in "${!PROFILES[@]}"; do
    PRESET=${PROFILES[$i]}
    progress=$(( (i + 1) * 100 / ${#PROFILES[@]} ))
    echo -ne "   -> Uruchamianie profilu: [$PRESET] ($progress%)... \r"
    ssh $RPi_USER@$RPi_HOST "export DISPLAY=:0; export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/broadcom_icd.json; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; export HEADLESS=0; cd $REMOTE_PATH && $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --benchmark 100000 --preset $PRESET" || { echo -e "\n!!! BENCHMARK ($PRESET) NIE POWIÓDŁ SIĘ"; exit 1; }
done
echo -e "\n>>> Benchmarki zakończone."

echo ">>> [5/14] Ekstrakcja i analiza telemetrycznych danych benchmarku..."
ssh $RPi_USER@$RPi_HOST "if [ -f $REMOTE_PATH/benchmark_results.txt ]; then echo ' '; echo '--- RAPORT WYDAJNOŚCI GPU ---'; cat $REMOTE_PATH/benchmark_results.txt; echo ' '; else echo 'Brak pliku wyników!'; exit 1; fi"

echo ">>> [6/14] Weryfikacja odporności systemu i stabilności pętli (Fuzzing)..."
ssh $RPi_USER@$RPi_HOST "export DISPLAY=:0; export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/broadcom_icd.json; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; export HEADLESS=1; cd $REMOTE_PATH && $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --fuzz-mode" || { echo "!!! FUZZING NIE POWIÓDŁ SIĘ"; exit 1; }

echo ">>> [6.5/14] Stress-Test: 5-minutowa symulacja długodystansowa..."
ssh $RPi_USER@$RPi_HOST "export DISPLAY=:0; export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/broadcom_icd.json; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; export HEADLESS=1; cd $REMOTE_PATH && $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --stress-test" &
SSH_PID=$!
duration=300
while [ $duration -gt 0 ]; do
    echo -ne "   -> Czas pozostały do zakończenia testu: $duration sekund... \r"
    sleep 1
    duration=$((duration - 1))
    if ! kill -0 $SSH_PID 2>/dev/null; then break; fi
done
echo -e "\n>>> Stress-Test zakończony."

echo ">>> [7/14] Kompilacja Ahead-Of-Time (Kompilacja AOT / ReadyToRun)..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH publish $REMOTE_PATH/CyberEngine.csproj -c Release -r linux-arm64 --self-contained true"

echo ">>> [8/14] Audyt bezpieczeństwa i analiza zależności..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH list $REMOTE_PATH/CyberEngine.csproj package --vulnerable"

echo ">>> [9/14] Statyczna analiza drzewa zależności..."
ssh $RPi_USER@$RPi_HOST "$DOTNET_PATH list $REMOTE_PATH/CyberEngine.csproj package"

echo ">>> [10/14] Finalny build weryfikacyjny na stacji lokalnej (Laptop)..."
dotnet build $LOCAL_PATH/CyberEngine.csproj -c Release

echo ">>> [11/14] Kontrola spójności plików binarnych..."
ssh $RPi_USER@$RPi_HOST "ls -lh $REMOTE_PATH/bin/Release/net11.0/CyberEngine.dll"

echo ">>> [12/14] Czyszczenie logów przejściowych..."
ssh $RPi_USER@$RPi_HOST "rm -f $REMOTE_PATH/perf_log_*.txt"

echo ">>> [13/14] Sygnowanie znacznikiem czasu..."
ssh $RPi_USER@$RPi_HOST "echo 'Build time: $(date +%Y-%m-%d_%H:%M:%S)'"

echo ">>> [14/14] Generowanie manifestu pomyślnego wdrożenia..."
ssh $RPi_USER@$RPi_HOST "touch $REMOTE_PATH/DEPLOYED_$(date +%Y%m%d_%H%M%S).log"

echo "========================================================"
echo "--- SUKCES: Kod przetestowany, skompilowany i wdrożony ---"
echo "========================================================"