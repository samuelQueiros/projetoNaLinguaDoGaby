BEGIN;

INSERT INTO "Fornecedores" ("Id", "RazaoSocial", "NomeFantasia", "CnpjCpf", "InscricaoEstadual", "Endereco", "Telefone", "Email", "Observacoes")
VALUES ('a3a84187-9440-4970-a78f-b2e0b6f3bdd8', 'Asana, Inc.', 'Asana', 'PENDENTE-CNPJ-ASANA', NULL, '633 Folsom St., San Francisco, CA 94107 - Estados Unidos', NULL, NULL, 'CNPJ/CPF pendente de preenchimento — empresa estrangeira, documento não veio na fatura de origem. Edite este cadastro na tela Fornecedores assim que tiver o EIN/documento correto (o valor placeholder abaixo precisa ser substituído por um dado real, único).');

COMMIT;
