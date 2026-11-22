# Avaliação: migração de .NET e distribuição assinada

Registro da fase 4 do roteiro (22/11/2026). Nada foi migrado; isto é só a avaliação.

## .NET Framework 4.8 → .NET moderno

- **Hoje:** compila com o `csc.exe` do Windows (C# 5), sem SDK e sem NuGet. `Rodar.cmd` funciona em qualquer PC com Windows 10/11. Isso é o principal motivo para ficar como está.
- **O .NET 4.8** segue o ciclo de vida do Windows onde está instalado e não tem data de fim anunciada.
- **Migrar (net8/net10-windows, WPF)** traria C# atual, testes com `dotnet test` e publicação single-file. Custos: exige o SDK em cada PC (ou um build pronto vindo do CI), o atualizador (`tools/AtualizarTito`, `Compilar.ps1`) precisa ser refeito, e `System.Web.Extensions` (JavaScriptSerializer) precisa virar `System.Text.Json`.
- **Recomendação:** não migrar agora. Primeiro separar as responsabilidades (lembretes, armazenamento, IA) em classes próprias. Migrar só se aparecer uma necessidade concreta, como uma API do Windows só disponível no .NET moderno ou um empacotamento MSIX.

## Assinatura

- O pedido ao SignPath não teve resposta, e o uso do Tito é 100% local e pessoal, então a assinatura fica de lado.
- Compilar no próprio PC (`Rodar.cmd`) evita a marca da internet nos arquivos, mas não garante passar pelo Smart App Control. Se o Windows bloquear, use `scripts/Diagnostico.ps1`.
