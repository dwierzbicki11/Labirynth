#!/bin/bash
#set -e 

# Konfiguracja środowiska
LOCAL_PATH="/home/zonderq/Labirynth"
REMOTE_PATH="/home/zonderq/Labirynth"
RPi_USER="zonderq"
RPi_HOST="10.0.0.2"
DOTNET_PATH="/home/zonderq/.dotnet/dotnet"

# Opcje SSH zapobiegające zerwaniu sesji pod maksymalnym obciążeniem SoC
SSH_OPTS="-o ServerAliveInterval=15 -o ServerAliveCountMax=4"

echo "========================================================"
echo "--- FABRYKA: Rozpoczęto pełny cykl produkcyjny (AI-Optimized) ---"
echo "========================================================"

echo ">>> [1/19] Synchronizacja kodu źródłowego..."
rsync -avz --delete --exclude 'bin' --exclude 'obj' --exclude '.git' "$LOCAL_PATH/" "$RPi_USER@$RPi_HOST:$REMOTE_PATH/"

echo ">>> [2/19] Kompilacja shaderów na maszynie zdalnej (RPi)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "cd $REMOTE_PATH && chmod +x CompileShaders.sh && ./CompileShaders.sh"

echo ">>> [3/19] Transfer zwrotny (Pull) skompilowanych plików .spv na stację lokalną..."
rsync -avz "$RPi_USER@$RPi_HOST:$REMOTE_PATH/Shaders/*.spv" "$LOCAL_PATH/Shaders/"

echo ">>> [4/19] Audyt cyberbezpieczeństwa: Skanowanie podatności CVE w zależnościach NuGet..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "cd $REMOTE_PATH && $DOTNET_PATH list package --vulnerable"

echo ">>> [5/19] Budowanie nowej wersji silnika (Release)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "$DOTNET_PATH build $REMOTE_PATH/CyberEngine.csproj -c Release"

echo ">>> [6/19] Testy jednostkowe podsystemu logicznego (Logika)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "$DOTNET_PATH test $REMOTE_PATH/CyberEngine.csproj --configuration Release"

echo ">>> [7/19] Diagnostyka GPU: Czyszczenie logów benchmarku..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "cd $REMOTE_PATH && rm -f benchmark_results.txt"

# WYBUDZENIE EKRANU I ZDJĘCIE BLOKADY ENERGETYCZNEJ (DPMS)
echo "   -> [SYSTEM] Wybudzanie bufora ramki i wyłączanie DPMS na węźle..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export DISPLAY=:0; export XAUTHORITY=/home/$RPi_USER/.Xauthority; xset dpms force on; xset s noblank; xset s off; xset -dpms" || echo "   -> [OSTRZEŻENIE] Brak aktywnej sesji X11..."

# ZREDUKOWANA MACIERZ TESTOWA (Skupiona na stabilności rdzenia graficznego przy obciążeniu AI)
VECTORS=(
    "--preset low --vsync 0" 
    "--resolution 1024x768 --preset low --vsync 0"
    "--resolution 1024x768 --render-scale 0.5 --quality 0 --vsync 0"
    "--preset low --draw-distance 64.0 --fov 90.0 --vsync 0"
    "--vsync 1 --fps-limit 30 --preset low"
)

echo ">>> [8/19] Rozpoczęcie iteracji wektorów GPU (Low-Profile)..."
for i in "${!VECTORS[@]}"; do
    ARGS="${VECTORS[$i]}"
    echo "   -> Wektor [$((i+1))/${#VECTORS[@]}]: $ARGS"
    
    ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
    
    ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export DISPLAY=:0; export XAUTHORITY=/home/$RPi_USER/.Xauthority; export VK_ICD_FILENAMES=/usr/share/vulkan/icd.d/broadcom_icd.json; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; cd $REMOTE_PATH && flock -x -w 10 /tmp/cyberengine.lock $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --benchmark 5000 $ARGS"
done

echo ">>> [9/19] Raport Benchmarków..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "cat $REMOTE_PATH/benchmark_results.txt"

echo ">>> [10/19] Fuzzing (Pojedyncza Instancja - Wektor AI)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export DISPLAY=:0; export XAUTHORITY=/home/$RPi_USER/.Xauthority; cd $REMOTE_PATH && flock -x -w 10 /tmp/cyberengine.lock $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --fuzz-mode 5000"

echo ">>> [11/19] Stress-Test GPU (Pojedyncza Instancja)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export DISPLAY=:0; export XAUTHORITY=/home/$RPi_USER/.Xauthority; cd $REMOTE_PATH && flock -x -w 10 /tmp/cyberengine.lock $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --stress-test 10"

echo ">>> [12/19] Multi-Instance Stress-Test CPU (2x Headless Concurrent Execution)..."
# Redukcja z 4x do 2x instancji. 4x Phi-3 załadowane do RAM wywoła OOM Panic i zresetuje malinę.
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export HEADLESS=1; export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1; cd $REMOTE_PATH && \
    ($DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --stress-test 15 & \
     $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --stress-test 15 & \
     wait)"

echo ">>> [13/19] GOD-BOT TACTICAL TEST (100k Klatek)..."
# Właściwy test wytrzymałościowy modelu Phi-3, bota nawigacyjnego i zarządcy L1
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export HEADLESS=1; cd $REMOTE_PATH && flock -x -w 10 /tmp/cyberengine.lock $DOTNET_PATH exec bin/Release/net11.0/CyberEngine.dll --ai-test --benchmark 100000 --preset low --vsync 0"

echo ">>> [14/19] Kompilacja paczki samowystarczalnej (Publish)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "$DOTNET_PATH publish $REMOTE_PATH/CyberEngine.csproj -c Release -r linux-arm64 --self-contained true"

# ====================================================================
# SEKCJA: POST-PRODUKCJA I DYSTRYBUCJA
# ====================================================================

echo ">>> [15/19] Izolacja środowiska produkcyjnego (Deployment)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "rm -rf $REMOTE_PATH/ProdBuild && mkdir -p $REMOTE_PATH/ProdBuild && cp -a $REMOTE_PATH/bin/Release/net11.0/linux-arm64/publish/. $REMOTE_PATH/ProdBuild/ && cp -r $REMOTE_PATH/Models $REMOTE_PATH/ProdBuild/ && cp -r $REMOTE_PATH/Shaders $REMOTE_PATH/ProdBuild/"
echo ">>> [16/19] Nadawanie uprawnień wykonawczych binarce..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "chmod +x $REMOTE_PATH/ProdBuild/CyberEngine"

echo ">>> [17/19] Test dymny (Smoke Test) natywnego pliku wykonywalnego..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "pkill -9 -f '[C]yberEngine' || true"
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "export DISPLAY=:0; export XAUTHORITY=/home/$RPi_USER/.Xauthority; cd $REMOTE_PATH/ProdBuild && flock -x -w 10 /tmp/cyberengine.lock ./CyberEngine --benchmark 10 --preset low"

echo ">>> [18/19] Archiwizacja paczki dystrybucyjnej (Release TAR)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "cd $REMOTE_PATH && tar -czf CyberEngine_arm64_latest.tar.gz -C ProdBuild ."

echo ">>> [19/19] Czyszczenie artefaktów tymczasowych (Wipe /obj i /bin)..."
ssh $SSH_OPTS $RPi_USER@$RPi_HOST "rm -rf $REMOTE_PATH/bin $REMOTE_PATH/obj $REMOTE_PATH/ProdBuild"

echo ">>> Zakończono."
echo "========================================================"
echo "--- SUKCES: Cykl produkcyjny i testy klastrowe ukończone ---"
echo "========================================================"