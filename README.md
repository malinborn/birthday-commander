Как запустить тестовое окружение: 

1. Запустить docker-compose в BirthdayCommander.Tests
    ```bash
    docker compose -f docker-compose.test-db.yml up -d
    ```
2. Установить dotnet scripts если еще не установлен *(можно убрать флаг -g и тогда tool установится локально)* 
    ```bash
    dotnet tool install -g dotnet-script 
    ```
3. Добавьте dotnet scripts в PATH 
    ```bash
    cat << \EOF >> ~/.zprofile
    # Добавление средств пакета SDK для .NET Core
    export PATH="$PATH:/Users/maximkovalevskij/.dotnet/tools"
    EOF 
    ```
4. Перезапустите консоль или выполните `zsh -l` если используете zsh 
5. Запустите скрипт
    ```bash
    dotnet script generate-test-employees.csx 
    ```

6. Проинициализируйте user-secrets
   ```bash
   dotnet user-secrets init 
    ```
7. Создайте бота с доступом к личным сообщениям в Mattermost или Slack и положите секрет в user-secrets, также добавьте url вашего сервера и вебхук секрет
   ```bash
   dotnet user-secrets set "Mattermost:BotToken" "{TOKEN}"
   dotnet user-secrets set "Mattermost:ServerUrl" "https://{DOMAIN}"
   dotnet user-secrets set "Mattermost:WebHookSecret" "{WEBHOOK_SECRET}"
    ```