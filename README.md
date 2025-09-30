для запуска необходимо сперва запустить симулятор

в ubuntu ввести следующие команды:
cd /mnt/(PATH)/ardupilot
. venv/bin/activate
cd ArduCopter
../Tools/autotest/sim_vehicle.py -v ArduCopter --console --map --out 127.0.0.1:14550

после чего необходимо создать конфигурацию запуска нескольких проектов и выбрать Receiver и Sender
и запустить их одновременно

далее для начала работы приложений нужно нажать кнопки "Connect to SITL" и "Start listening"