# Guia completo para migrar o projeto para Vuforia no Unity

> **Histórico, não segue mais valendo.** O projeto continuou em **AR Foundation**, como pede o planejamento:
> a lupa física é reconhecida por um marcador preso no cabo (veja o [README](README.md)). O Vuforia exigiria
> conta e licença, e o pacote `.tgz` dele nunca foi para o repositório (estava no `.gitignore`).

Este guia foi pensado para você que ainda está começando no Unity e quer fazer a migração do projeto de realidade aumentada de AR Foundation para Vuforia de forma organizada.

Objetivo: migrar o projeto para uma única tecnologia, sem misturar AR Foundation e Vuforia no mesmo projeto.

## 1) Antes de começar

Antes de instalar qualquer coisa, é importante entender a mudança de arquitetura que você quer fazer:

- O projeto atual usa AR Foundation para rastrear imagens/QR Code.
- O novo conceito do jogo quer rastrear a lupa real, sem QR Code adicional na lupa.
- Como a lupa é um objeto físico real, o Vuforia é mais adequado para esse tipo de rastreamento.
- A ideia é manter o projeto todo em Vuforia para simplificar a estrutura e a explicação para o professor.

### Recomendação prática
Se você ainda não tem o projeto muito avançado em cenas e scripts, a migração é mais fácil agora do que depois.

Se o projeto estiver em Unity 6 e o Vuforia der problema de compatibilidade, use uma versão LTS mais estável do Unity, por exemplo:
- Unity 2022.3 LTS

Isso não significa que seu projeto precise ser refeito do zero; apenas indica que, em alguns casos, a versão do Unity pode causar conflitos com alguns pacotes.

---

## 2) Preparação do ambiente

### 2.1 Verifique seu Unity
Você já tem o Unity instalado, então o próximo passo é confirmar se ele está funcionando corretamente:

1. Abra o Unity Hub.
2. Verifique se a versão instalada aparece corretamente.
3. Se possível, clique em "Instalar Editor" e confirme se existe alguma versão LTS disponível.
4. Se o Vuforia causar erro de compatibilidade, troque para a versão LTS mais estável.

### 2.2 Faça backup do projeto atual
Antes de mexer em qualquer coisa:

1. Copie a pasta do projeto para outra pasta.
2. Faça um backup no Google Drive, OneDrive ou pendrive.
3. Faça uma cópia da pasta do Unity antes da migração.

Isso é importante porque qualquer mudança de pacote pode causar erros ou quebrar a cena.

---

## 3) Criar conta e licença no Vuforia

### 3.1 Acesse o site do Vuforia
Abra no navegador:

https://developer.vuforia.com/

### 3.2 Crie uma conta
Se não tiver conta:

1. Clique em "Register" ou "Criar conta".
2. Preencha os dados de cadastro.
3. Confirme o e-mail.

### 3.3 Gere a licença do Vuforia
Depois de entrar:

1. Vá em "Develop" ou "Desenvolvimento".
2. Entre na área de licenciamento.
3. Crie um novo projeto/licença.
4. Copie a chave da licença.

Esse código será usado depois dentro do Unity.

> Importante: a licença precisa estar ativa para o projeto funcionar no editor e em build para celular.

---

## 4) Instalar o Vuforia no Unity

### 4.1 Abra o projeto Unity
Abra o projeto do seu jogo:

- `CircuitAIR`

### 4.2 Instale o package do Vuforia
Existem duas formas principais:

#### Opção A: via Package Manager (mais fácil)
1. Abra o Unity.
2. Vá em `Window` > `Package Manager`.
3. Clique no botão `+` no canto superior esquerdo.
4. Selecione `Add package by name...`.
5. Digite:
   `com.ptc.vuforia.engine`
6. Clique em `Add`.
7. Aguarde a instalação.

#### Opção B: via arquivo .unitypackage
Se a versão do pacote estiver em arquivo local:

1. Vá em `Assets` > `Import Package` > `Custom Package`.
2. Selecione o arquivo `.unitypackage` do Vuforia.
3. Confirme a importação.

### 4.3 Verifique se o pacote foi instalado
Depois da instalação, confirme:

- no Project, apareceu uma pasta com Vuforia
- no menu `Window`, aparece alguma opção de Vuforia
- o projeto ainda abre corretamente

Se aparecer erro, feche e abra o Unity novamente.

---

## 5) Configurar a licença do Vuforia

### 5.1 Ative a licença no Unity
Dentro do Unity:

1. Vá em `Edit` > `Project Settings`.
2. Procure pela opção `Vuforia Configuration` ou `Vuforia`.
3. Cole a licença que você gerou no site do Vuforia.
4. Salve.

Se a opção não aparecer, pode ser que o pacote ainda não tenha sido importado totalmente ou que o Unity precise ser reiniciado.

### 5.2 Verifique o status
Depois de colar a licença:

- a licença deve ficar validada
- não deve aparecer erro de licença

---

## 6) Entender o que realmente vai mudar no projeto

A partir deste ponto, a mudança é mais conceitual do que instalação.

### 6.1 O que está sendo migrado
Seu projeto atual usa:
- `ARTrackedImageManager`
- `ARTrackedImagesChangedEventArgs`
- `UnityEngine.XR.ARFoundation`

Esses scripts fazem rastreamento por imagem/QR Code.

No Vuforia, o fluxo vai ser diferente:
- você cria alvos (targets)
- você associa cada alvo a um objeto na cena
- o Vuforia detecta o alvo pela câmera
- o objeto virtual na cena é posicionado em relação a esse alvo

### 6.2 O que continua igual
A lógica do jogo pode continuar basicamente a mesma:
- tempo
- erros
- pontuação
- reinício
- vitória e derrota
- circuito visual

O que muda é a forma como a câmera reconhece o circuito e a lupa.

---

## 7) Criar os alvos no Vuforia

### 7.1 O que você vai usar como alvo
Você precisa decidir o que o Vuforia vai rastrear.

No seu cenário, há duas possibilidades:

#### Opção 1: Usar o QR Code como target principal
- O QR Code é o alvo que posiciona o circuito virtual.
- Essa parte é simples e funciona muito bem com Vuforia.

#### Opção 2: Usar a lupa como target principal
- A lupa real aparece na câmera.
- O Vuforia reconhece o desenho/objeto da lupa.
- O objeto virtual da lupa é posicionado em relação à lupa real.

### 7.2 Melhor abordagem para o seu projeto
A abordagem mais coerente com seu jogo é:

- usar o QR Code para posicionar o circuito virtual
- usar a lupa como objeto físico que será rastreado para interactuar com o circuito

Se a lupa for muito complexa para o Vuforia reconhecer diretamente, você pode usar uma imagem de referência da lupa ou uma marca mais simples da mesma.

### 7.3 Como criar um target no Vuforia
No site do Vuforia:

1. Crie um novo alvo no projeto da licença.
2. Faça upload do QR Code ou imagem da lupa.
3. Aguarde o processamento da imagem.
4. Confirme se o alvo ficou bom e está estável.
5. Faça download do database gerado.

Depois de pronto:

1. Abra o Unity.
2. Importe o database do Vuforia.
3. O Vuforia vai reconhecer esse alvo na cena.

---

## 8) Configurar a cena no Unity para Vuforia

### 8.1 Estrutura básica da cena
A cena deve ter algo assim:

- `AR Camera` (ou `Main Camera` com Vuforia)
- `ImageTarget` ou `ObjectTarget`
- `Circuito virtual`
- `Lupa virtual`
- `GameManager`
- `UI` do jogo

### 8.2 Como criar a câmera do Vuforia
Em Unity:

1. Crie uma cena nova ou use a cena atual.
2. No topo da hierarquia, procure por `Main Camera`.
3. Configure a camera para usar Vuforia.
4. Adicione a lógica do Vuforia a essa câmera.
5. Confirme que a câmera consegue ativar a realidade aumentada.

> Se a câmera não estiver funcionando, o problema geralmente é licença, pacote ou camera settings.

### 8.3 Criar o alvo
No Unity:

1. Crie um GameObject vazio chamado `QR_Target` ou `CircuitTarget`.
2. Adicione o componente do Vuforia.
3. Selecione o target que você carregou.
4. Configure a escala e a posição.

### 8.4 Posicionar o circuito
Depois de criar o target:

1. Coloque o circuito virtual como filho do target.
2. Ajuste a posição em relação ao QR Code.
3. Ajuste a escala para ficar proporcional à imagem real.
4. Teste com a câmera do celular.

---

## 9) Adaptar os scripts do projeto

Agora vem a parte mais importante da migração: os scripts.

### 9.1 O script de rastreamento em AR Foundation
Atualmente, o script mais importante é:

- `CircuitARController.cs`

Esse script usa:
- `ARTrackedImageManager`
- `UnityEngine.XR.ARFoundation`

Esse trecho será removido ou substituído.

### 9.2 A lógica que deve permanecer
A lógica que você provavelmente pode manter quase igual é:

- `CircuitGameManager.cs`
- `CircuitZone.cs`
- `CircuitLayoutBuilder.cs`
- `LupaTip.cs`

Esses scripts tratam de:
- tempo
- erros
- colisão
- vitória
- começo e fim da partida

O grande ajuste é a forma como o objeto é rastreado.

### 9.3 O que você deve trocar no código
No lugar de `ARTrackedImageManager`, o Vuforia usa APIs como:

- `DefaultTrackableEventHandler`
- `ImageTargetBehaviour`
- `ObjectTargetBehaviour`
- `TrackableBehaviour`

Em outras palavras:

- em vez de escutar `trackedImagesChanged`
- você passa a escutar quando um alvo foi encontrado ou perdido

### 9.4 O que fazer com os scripts
Você vai precisar:

1. remover os imports do `UnityEngine.XR.ARFoundation`
2. importar o namespace do Vuforia
3. criar um script que detecte quando o alvo foi encontrado
4. atualizar a posição do circuito e objeto virtual
5. reaproveitar o restante da lógica do jogo

### 9.5 Regra principal
Não é necessário reescrever tudo do zero.

Você pode manter quase toda a lógica do game e apenas substituir a parte de rastreamento.

---

## 10) Estrutura sugerida do projeto depois da migração

Uma estrutura boa para o projeto ficar mais organizada seria:

```text
Assets/
  CircuitAIR/
    Scripts/
      Game/
        CircuitGameManager.cs
        CircuitZone.cs
        CircuitLayoutBuilder.cs

      AR/
        VuforiaCircuitController.cs
        VuforiaLupaController.cs

      Interaction/
        LupaTip.cs

    Prefabs/
      Circuito
      Lupa

    Targets/
      QRCode
      LupaTarget
```

Assim, fica mais fácil entender o projeto e manter o padrão acadêmico parecido com o do professor.

---

## 11) Como migrar a cena do Unity na prática

### Passo a passo real do editor
1. Abra a cena atual.
2. Remova a câmera AR Foundation antiga, se houver.
3. Crie ou configure a câmera Vuforia.
4. Crie o target do QR Code.
5. Crie o target da lupa.
6. Posicione o circuito virtual como filho do target do QR Code.
7. Posicione a lupa virtual como filho do target da lupa.
8. Configure o script de controle do circuito.
9. Ajuste a escala dos objetos.
10. Teste a camera em real-time.

### Importante
Se a lupa não estiver alinhando corretamente com o objeto real, a correção é ajustar:
- posição local do objeto
- rotação do objeto
- escala do modelo
- ponto de referência da ponta da lupa

---

## 12) Testar a migração

Depois de configurar tudo:

### 12.1 Teste no editor
1. Aperte play na Unity.
2. Veja se o target do QR Code é reconhecido.
3. Veja se o circuito aparece sobre o QR Code.
4. Veja se a lupa reconhece o alvo.
5. Teste se o objeto virtual da lupa está no lugar esperado.

### 12.2 Teste no celular
1. Faça build para Android.
2. Instale no celular.
3. Posicione o QR Code e a lupa no ambiente.
4. Verifique se o circuito aparece no lugar correto.
5. Verifique se o jogo funciona.

### 12.3 Ajustes finais
Você provavelmente vai precisar:

- ajustar a escala da lupa
- ajustar a posição da ponta da lupa
- recalibrar a colisão
- testar várias vezes a borda do circuito
- ajustar a sensibilidade dos erros

---

## 13) Problemas comuns e como resolver

### Problema: o circuito não aparece no QR Code
Causas possíveis:
- target não importado corretamente
- licença inválida
- camera não configurada
- target não associado ao GameObject correto

Solução:
- verificar a licença Vuforia
- verificar o database importado
- verificar se o target foi associado ao objeto correto

### Problema: a lupa não é reconhecida
Causas possíveis:
- imagem da lupa muito complexa
- alvo fraco/sem contraste suficiente
- modelo real não combina com a imagem usada para treino

Solução:
- usar uma imagem mais simples ou mais alta qualidade
- testar outros ângulos da lupa
- criar alvo mais robusto

### Problema: colisão funciona ruim
Causas possíveis:
- ponta da lupa não está alinhada com obj real
- colisão muito sensível ou pouco sensível
- modelo em escala errada

Solução:
- ajustar a escala e a posição do trigger
- criar um ponto mais preciso para a ponta
- testar com o objeto real em várias posições

---

## 14) Checklist final antes de entregar o projeto

Antes de apresentar para o professor, verifique:

- [ ] Projeto está totalmente em Vuforia
- [ ] Não há mistura de AR Foundation e Vuforia
- [ ] QR Code ou alvo principal está funcionando
- [ ] Lupa está sendo rastreada
- [ ] Circuito aparece corretamente sobre o alvo
- [ ] Colisão com borda funciona
- [ ] Contador de erros está funcionando
- [ ] Tempo funciona corretamente
- [ ] Tela final aparece com resultado
- [ ] O projeto consegue rodar no celular
- [ ] O código está organizado e legível
- [ ] O README está atualizado

---

## 15) O que você precisa fazer agora, em ordem

### Ordem correta para você seguir
1. Fazer backup do projeto.
2. Instalar o Vuforia no Unity.
3. Criar a licença no Vuforia.
4. Importar o pacote no Unity.
5. Configurar a licença no projeto.
6. Criar os alvos do QR Code e da lupa.
7. Criar a cena com a câmera e objetos.
8. Posicionar o circuito sobre o alvo.
9. Ajustar a lógica da lupa e colisão.
10. Migrar os scripts de rastreamento.
11. Testar em editor.
12. Testar no celular.
13. Ajustar escalas, colisões e feedback visual.
14. Documentar o projeto.

---

## 16) Minha recomendação final

Você está no caminho certo ao decidir migrar tudo para Vuforia, porque o conceito atual do projeto deixou claro que o foco não é mais apenas rastrear uma imagem simples. O projeto passou a depender de reconhecimento mais real do objeto físico e da interação com a lupa, e isso é mais compatível com Vuforia.

A migração total deixa o projeto mais coerente, mais fácil de explicar e menos sujeito a inconsistências entre duas tecnologias diferentes.

---

## 17) O que eu posso fazer por você a seguir

Eu consigo continuar te ajudando em etapas bem práticas, por exemplo:

1. criar a checklist de migração em formato de tarefas
2. te entregar o mapa dos scripts a trocar
3. escrever o código base para Vuforia
4. te mostrar como implementar a lógica de rastreamento da lupa
5. te ajudar a montar a cena no Unity passo a passo
6. revisar sua metodologia antes de testar no celular

Se quiser, no próximo passo eu posso te entregar uma versão mais prática ainda, em formato de checklist de execução do dia-a-dia, para você seguir diretamente no Unity sem perder tempo.
