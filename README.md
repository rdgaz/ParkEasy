# ParkEasy — Sistema de Gerenciamento de Estacionamento

Sistema desktop moderno, ágil e robusto para controle de entrada e saída de veículos em estacionamentos, desenvolvido em **C#**, **.NET 10**, **Windows Forms**, **Entity Framework Core** e **SQLite**.

---

## 📌 Requisitos do Sistema

- **Sistema Operacional:** Windows 10 / 11 (x64)
- **Runtime/SDK:** .NET 10.0 SDK ou superior
- **Impressora Térmica:** Bematech MP-4200 TH (driver de Spooler do Windows instalado) ou modo Mock/Desenvolvimento.

---

## 🚀 Instalação e Execução

### 1. Clonar o repositório
```bash
git clone <URL_DO_REPOSITORIO>
cd ParkEasy
```

### 2. Restaurar dependências e compilar
```bash
dotnet restore
dotnet build --configuration Release
```

### 3. Executar o sistema
```bash
dotnet run --project src/ParkEasy.UI/ParkEasy.UI.csproj
```

---

## 🗄️ Banco de Dados SQLite

O sistema utiliza um banco de dados local **SQLite** armazenado no arquivo:
```text
parking.db
```

- O banco de dados e suas tabelas/índices são **criados automaticamente** na primeira execução.
- Não exige instalação ou configuração de servidor de banco de dados.

---

## ⚙️ Configuração (`appsettings.json`)

As configurações de negócio e de impressora ficam no arquivo `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=parking.db"
  },

  "Parking": {
    "TotalSpaces": 50
  },

  "Pricing": {
    "GracePeriodMinutes": 10,
    "DailyMaximum": 50.00,
    "Moto": {
      "FirstHour": 5.00,
      "AdditionalHour": 3.00
    },
    "Carro": {
      "FirstHour": 10.00,
      "AdditionalHour": 5.00
    },
    "VagaDupla": {
      "FirstHour": 15.00,
      "AdditionalHour": 8.00
    }
  },

  "Printer": {
    "Type": "Mock",
    "WindowsPrinterName": "Bematech MP-4200 TH"
  }
}
```

### Opções de Preço
- `GracePeriodMinutes`: Período de tolerância em minutos (ex: 10 minutos = R$ 0,00), válido para todos os tipos de veículo.
- `DailyMaximum`: Teto máximo diário de cobrança, válido para todos os tipos de veículo.
- `Moto` / `Carro` / `VagaDupla`: cada tipo de veículo tem seu próprio `FirstHour` (valor cobrado até a primeira hora após a tolerância) e `AdditionalHour` (valor cobrado por hora, ou fração de hora, adicional). `VagaDupla` representa veículos que ocupam duas vagas físicas (ex: caminhonetes, vans) e conta como 2 vagas no painel de ocupação.

---

## 🖨️ Configuração e Teste da Impressora Bematech MP-4200 TH

### 1. Instalação da Impressora
1. Conecte a impressora Bematech MP-4200 TH via USB ou Serial/Ethernet.
2. Instale o driver de spooler oficial da Bematech para Windows (*Bematech Spooler Driver*).
3. Verifique o nome com o qual a impressora foi instalada no Windows (ex: `Bematech MP-4200 TH`).

### 2. Configurar o `appsettings.json`
Para utilizar a impressora física, altere a seção `Printer` para:
```json
"Printer": {
  "Type": "BematechMP4200TH",
  "WindowsPrinterName": "Bematech MP-4200 TH"
}
```

### 3. Teste de Impressão no Sistema
- Abra o sistema.
- Acesse o menu **Sistema ➔ Testar Impressora**.
- O sistema enviará um comprovante de teste via comandos nativos **ESC/POS** para a impressora.

> **Modo de Desenvolvimento:** Quando `Type` estiver configurado como `"Mock"`, os tickets e comprovantes serão registrados no log do console/aplicação sem depender de impressora física.

---

## 🧪 Executando os Testes Unitários

O projeto inclui um conjunto completo de testes automatizados xUnit.

Para executar todos os testes:
```bash
dotnet test
```

Os testes cobrem:
- **Cálculo de Tarifas:** Tolerância, 1ª hora, horas fracionadas, teto diário e configurações customizadas.
- **Serviço de Estacionamento:** Registro de entrada, validação e bloqueio de placa duplicada ativa, geração de ticket único, finalização e prevenção de duplicidade.
- **Normalização de Placas:** Padrão antigo (ABC1234) e Mercosul (ABC1D23).
- **Serviço de Impressão Mock:** Registro de chamadas e tickets impressos.

---

## 💾 Backup do Banco de Dados

Para realizar backup dos dados:
1. Acesse o menu **Sistema ➔ Fazer Backup**.
2. Escolha o local para salvar o arquivo de backup (sugestão de nome automática: `parking_backup_AAAA-MM-DD_HHMMSS.db`).
3. O arquivo original `parking.db` **nunca é alterado ou sobrescrito** durante o processo.
4. Para visualizar seus backups, acesse **Sistema ➔ Abrir Pasta de Backups**.

---

## 📦 Publicação Self-Contained (Release)

Para gerar uma versão executável independente (que não exige .NET pré-instalado na máquina do cliente):

```bash
dotnet publish src/ParkEasy.UI/ParkEasy.UI.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

O executável gerado estará na pasta:
`src/ParkEasy.UI/bin/Release/net10.0-windows/win-x64/publish/`

---

## ⌨️ Atalhos do Teclado

- `F2`: Registrar Nova Entrada
- `F4`: Focar no campo de Pesquisa
- `F5`: Atualizar Lista de Veículos
- `Enter`: Confirmar Ação/Formulário
- `Esc`: Cancelar/Fechar Diálogo
