# Example Sword - Teste Android 0.1.1

Mod pequeno para testar importacao e carregamento no tML Android 0.2.14
(tModLoader privado 9999.0 / Terraria PC 1.4.5.8 / .NET 10).

## Testar

1. No launcher, toque em Adicionar mods (.tmod) e selecione AndroidSwordTest_0.1.1.tmod.
2. Inicie o jogo, abra Gerenciar mods, ative Example Sword - Teste Android e recarregue os mods.
3. Entre no mundo e tenha 1 bloco de terra no inventario.
4. Abra o inventario e crie a Espada de Exemplo. Nao precisa de bancada.

A espada tem 50 de dano base, particulas Sparkle e aplica Em Chamas por 1 segundo.
Nao adiciona chefes, biomas, geracao de mundo ou bibliotecas nativas.
O pacote ja contem a DLL compilada: nao requer SDK/Roslyn instalado no celular.
O log de Load informa que o mod foi carregado e mostra a receita.

## Base e creditos

Codigo e texturas da ExampleSword, Sparkle, ExampleModRarity e
ExampleHigherTierModRarity: tModLoader Team, ExampleMod oficial, commit
20a7d832721bdc7fd62b42c0aa00fe26db409a6b.

https://github.com/tModLoader/tModLoader/blob/20a7d832721bdc7fd62b42c0aa00fe26db409a6b/ExampleMod/Content/Items/Weapons/ExampleSword.cs

Adaptacao de teste: namespace separado, receita de 1 terra no lugar dos itens e
bancada do ExampleMod, localizacao PT-BR/EN e log de carregamento. Dano, velocidade,
particulas, fogo, texturas e raridades do exemplo foram mantidos.
A licenca original acompanha o mod em LICENSE.

## Compilar no computador

Na raiz deste repositorio, com o SDK .NET 10 e a pasta privada do jogo preparada:

```
dotnet build examples/AndroidSwordTest -c Release -p:GameDirectory=/caminho/game
dotnet run --project tools/Tml.ModPackage -c Release -- /caminho/game examples/AndroidSwordTest /caminho/AndroidSwordTest_0.1.1.tmod
```

A ferramenta usa o TmodFile e BuildProperties da DLL real do jogo para escrever e
reabrir o pacote. Confere hash, metadados, DLL e heranca ModItem. Esses testes locais
nao substituem o primeiro carregamento no Android.

## Correcao 0.1.1

Inclui icon.png (80x80) e icon_small.png (30x30) oficiais do ExampleMod,
exigidos pela etapa de autoload do cliente. O empacotador verifica presenca,
assinatura PNG e dimensoes dos dois icones. Nao requer atualizar o APK 0.2.14.
