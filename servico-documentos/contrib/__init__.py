"""Implementações de REFERÊNCIA das etapas 2 (confirmação) e 3 (envio ao ERP).

No ERP de produção essas duas etapas vivem no backend .NET — ver
`docs/modulo-ia-documentos.md`. O que está aqui existe só para:
  - rodar o `exemplo_uso.py` ponta a ponta sem subir o .NET;
  - servir de espec executável para quem for implementar o lado .NET.
Nada em `contrib/` é importado por `app/`.
"""
