# tm

### Create the Trusted Signing resources in Azure
1.	Create a Trusted Signing account in a supported region.
2.	Create a Public Trust identity-validation request for your individual or organization.
3.	After Microsoft approves the identity, create a certificate profile using that identity.
4.	Note these values:
•	Trusted Signing account name
•	Certificate profile name
•	Endpoint, for example: https://eus.codesigning.azure.net/

Identity validation can take time. Use the real publisher/organization details you want Windows users to see.

### Let GitHub Actions authenticate to Azure
Create an Azure app registration/service principal for GitHub Actions and configure an OIDC federated credential for this repository and release workflow. This avoids storing an Azure client secret in GitHub.
Assign that app the Trusted Signing Certificate Profile Signer role at the Trusted Signing account scope.
Add these GitHub repository secrets or variables:
•	AZURE_CLIENT_ID
•	AZURE_TENANT_ID
•	AZURE_SUBSCRIPTION_ID
•	TRUSTED_SIGNING_ACCOUNT
•	TRUSTED_SIGNING_PROFILE

### 3. Add signing after dotnet publish
 ```
 - name: Publish
        shell: pwsh
        run: |
          dotnet publish .\TM\TM.csproj `
            --configuration Release `
            --runtime win-x64 `
            --output .\artifacts\publish

      - name: Sign in to Azure with OIDC
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Sign published executable
        uses: azure/trusted-signing-action@v0.5.9
        with:
          endpoint: https://eus.codesigning.azure.net/
          trusted-signing-account-name: ${{ secrets.TRUSTED_SIGNING_ACCOUNT }}
          certificate-profile-name: ${{ secrets.TRUSTED_SIGNING_PROFILE }}
          files-folder: ${{ github.workspace }}\artifacts\publish
          files-folder-filter: exe
          files-folder-recurse: true
          file-digest: SHA256
          timestamp-rfc3161: http://timestamp.acs.microsoft.com
          timestamp-digest: SHA256

      - name: Verify signature
        shell: pwsh
        run: |
          & "C:\Program Files (x86)\Windows Kits\10\bin\x64\signtool.exe" `
            verify /pa /v .\artifacts\publish\TM.exe
   ```
