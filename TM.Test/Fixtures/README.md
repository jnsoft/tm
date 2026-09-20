# Frozen WPF compatibility fixture

These files contain **synthetic, public test data only**, not user documents or production secrets.

- `baseline-encrypted.xml`: generated once from unchanged production code at `cf0a98b154b06cb7529e6a0c2433d543e3875174`, with jnUtil 2.0.2 on Windows.
- Password: `baseline-test-only` (deliberately public).
- `baseline-expected.xml`: expected decrypted projects, including nested protected items, IDs, priorities, dates and values from `TestDataBuilder.CreateSampleProjects()`.
- `baseline-private-key.sha256`: digest of the synthetic ECDH private key; verifies persistence independently of a newly generated key.
- The CA subject is `TM synthetic compatibility fixture`; its private key is test material only. Never trust or install this certificate.

The disposable generator called `CreateLoadedEncryptedDocument`, `GenerateKeys`, set a synthetic CA via `X509Helper.CreateCACert`, and saved `GetAsEncryptedXml`, `GetUnencryptedXml` and SHA256 of `GetUnprotectedPrivateKey`. Random IDs, salts, timestamps and keys were generated once and are now frozen. Unicode/XML-special character behavior is covered separately by a dynamic test.

Tests embed and load these files; they must NOT regenerate them. Do not replace a fixture to make a new reader pass: preserving a baseline reader contract is their purpose. Changes need an explicit format compatibility decision and a new fixture, retaining the old one.
