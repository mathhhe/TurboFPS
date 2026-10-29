# TurboFPS: otimizador de PC para Windows 10 e 11

**Português** · [English](README.en.md)

![Tela do TurboFPS](docs/tela.png)

Programa único (`TurboFPS.exe`, ~120 KB) com **77 opções**: 64 otimizações reversíveis e 13 ferramentas de limpeza e manutenção. Não precisa instalar nada, porque usa o .NET Framework 4.8 que já vem com o Windows.

**Download:** pegue o `TurboFPS.exe` na página de [Releases](../../releases/latest). Ele é compilado automaticamente pelo GitHub Actions a partir deste código, e o arquivo `.sha256` ao lado serve para conferir que é o mesmo arquivo.

**Por que confiar:** o código é curto e aberto, cada mudança guarda o valor original para o botão Reverter, e o programa nunca desliga Defender, firewall, UAC ou atualizações de segurança.

## Como usar

1. Abra o `TurboFPS.exe`. Ele pede permissão de administrador, porque mexe em registro, serviços e plano de energia.
2. Escolha um **perfil rápido** à esquerda ou marque as opções uma por uma:
   - **Recomendado (seguro)**: só itens de risco baixo. Em notebook, deixa de fora o que gasta mais bateria.
   - **Gamer (máximo FPS)**: risco baixo e médio.
   - Itens de **risco alto** nunca são marcados automaticamente e pedem confirmação.
3. Clique em **Aplicar selecionadas**. Por padrão, um ponto de restauração do Windows é criado antes.
4. Reinicie quando o programa pedir. Os itens com a etiqueta "REINÍCIO" só valem depois disso.

**Para desfazer**, marque o item (status "Ativo") e clique em **Reverter**. Antes de mudar qualquer coisa, o programa salva o valor original em `C:\ProgramData\TurboFPS\backup.txt`, e o Reverter devolve exatamente esse valor. O histórico de ações fica em `C:\ProgramData\TurboFPS\log.txt`.

## As 77 opções

### Jogos (13)
| Opção | Risco |
|---|---|
| Ativar Modo de Jogo | Baixo |
| Desativar gravação em segundo plano (Game DVR) | Baixo |
| Desativar sobreposição do Xbox Game Bar | Baixo |
| Agendamento de GPU acelerado por hardware (HAGS) | Médio |
| Desativar otimizações de tela cheia | Médio |
| Prioridade máxima de CPU e GPU para jogos (MMCSS) | Baixo |
| Reservar menos CPU para tarefas em segundo plano | Baixo |
| Remover limitação de rede durante multimídia | Baixo |
| Priorizar o programa em primeiro plano | Baixo |
| Desativar aceleração do mouse | Baixo |
| Desativar atalhos de Teclas de Aderência e Filtro | Baixo |
| Desativar sobreposição multiplano (MPO) | Médio |
| Desativar Integridade de Memória (VBS/HVCI) | **Alto** |

### Energia (9)
Plano Desempenho Máximo · Desativar Power Throttling · Desativar estacionamento de núcleos · CPU sempre em frequência máxima (médio) · Desativar suspensão seletiva de USB · Desativar economia de energia do PCIe · Nunca desligar os discos · Desativar hibernação · Desativar Inicialização Rápida

### Visual (7)
Efeitos visuais no modo desempenho (mantém o ClearType) · Sem animações de minimizar/maximizar · Sem animações na barra de tarefas · Sem transparência · Sem Aero Peek · Menus instantâneos · Sem sombras e seleção translúcida

### Privacidade e segundo plano (12)
Telemetria · ID de anúncios · Apps da Store em segundo plano (médio) · Dicas, sugestões e apps instalados sozinhos · Pesquisa Bing no Iniciar · Cortana · Histórico de atividades · Relatório de erros · Coleta de digitação e escrita · Pedidos de feedback · Notícias e Interesses/Widgets · Copilot

### Serviços (12)
DiagTrack · SysMain (médio) · Windows Search (médio) · Spooler de impressão (médio) · Fax · Mapas offline · WAP Push · Registro Remoto · Modo loja (RetailDemo) · Hub de Diagnóstico · Compartilhamento do WMP · Relatório de Erros (WerSvc)

### Rede (4)
Desativar Nagle (menos ping em jogos TCP) · Priorizar resolução DNS · Remover reserva de banda do QoS · Desativar envio P2P de atualizações

### Sistema (7)
Apps de inicialização sem atraso · Desligamento mais rápido · Kernel sempre na RAM · Sem registro de último acesso NTFS · Sem nomes curtos 8.3 · Sem reinício automático do Windows Update · Sem AutoPlay

### Ferramentas (13), que rodam na hora
Limpar temporários · Limpar cache do Windows Update · Limpar cache de shaders (DirectX/NVIDIA/AMD) · Limpar miniaturas · Limpar relatórios de erro e dumps · Esvaziar Lixeira · Limpar cache DNS · Criar ponto de restauração · Otimizar disco (TRIM/desfragmentar) · Abrir Limpeza de Disco · Gerenciar apps de inicialização · Escolher GPU por jogo · Reparar Windows (DISM + SFC)

## Avisos honestos

- **O ganho de FPS varia muito.** Em PC já bem configurado, a diferença costuma ser pequena. O que mais ajuda é o Game DVR desligado, o plano de energia, o MMCSS e menos coisa rodando em segundo plano. Nenhum programa substitui driver de vídeo atualizado e boa refrigeração.
- **Integridade de Memória (risco alto)** deixa o PC menos protegido, e alguns anti-cheats (Valorant/Vanguard, FACEIT) exigem que ela esteja ligada.
- O programa **não** desativa Windows Defender, firewall, UAC nem atualizações de segurança, de propósito.
- Por não ter assinatura digital, o Windows SmartScreen pode avisar "O Windows protegeu o computador". Para abrir, clique em *Mais informações → Executar assim mesmo*.
- Os ajustes "do usuário" (mouse, visual, privacidade) valem para a conta que abriu o programa. Se você eleva usando a senha de *outra* conta de administrador, eles vão para essa outra conta.

## Compilar de novo

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

O código está em `src/`. Para adicionar uma otimização nova, inclua uma entrada em `src/Catalog.cs`. A interface, o backup e o Reverter já funcionam para ela automaticamente.

Para conferir sem alterar nada (só leitura), rode `TurboFPS.exe --selftest relatorio.txt`. Ele lista o estado de cada otimização no PC.

Para publicar uma versão nova, crie e envie uma tag (`git tag v1.0.0` e depois `git push origin v1.0.0`). O GitHub Actions compila, roda o autoteste e anexa o .exe à Release.

## Licença

[MIT](LICENSE). Use, modifique e distribua à vontade. O programa é fornecido sem garantia; use por sua conta e risco.
