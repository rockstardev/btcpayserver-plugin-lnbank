# Troubleshooting

## Debugging connection problems

On the Lightning node connection setting screen, switch to "Use custom node".
There you will find the LNbank connection string, which looks like this:

```
type=lnbank;server=https://mybtcpay.com/;api-token=WALLET_ACCESS_KEY
```

On that view you can also use the "Test connection" functionality.
If you encounter problems like "The Lightning node did not reply in a timely manner", it's most likely a DNS-related problem.

### DNS problems

The server that BTCPay is running on might not be able to resolve the domain (in this example `mybtcpay.com`) correctly.

Use the ping command to debug to the problem.
`ping mybtcpay.com` should point to the IP of your server:

```bash
$ ping mybtcpay.com
PING mybtcpay.com (XX.XX.XX.XX) 56(84) bytes of data.
64 bytes from XX.XX.XX.XX (XX.XX.XX.XX): icmp_seq=1 ttl=52 time=263 ms
```

The `XX.XX.XX.XX` should be the external IP of the server.
In case you don't know it, run the same command not from your server, but from you local computer — this should give you the public IP of ythe server.
It the server's DNS resolves the domain to a local IP, find out where that is defined (most likely in the `/etc/hosts` file) and remove that mapping.

### For Cloudflare users

If you are using Cloudflare, check if changing the folowing settings makes a difference:

- Disable the [Bot Fighter Mode](https://developers.cloudflare.com/bots/get-started/free/)
- Switch the [Proxy Status](https://developers.cloudflare.com/dns/manage-dns-records/reference/proxied-dns-records) to "Proxied" instead of "DNS only"

### Using cURL for debugging

You can also try to access the LNbank node info via cURL, which gives you a verbose output of possible connection problems:

```bash
curl -vvv \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer WALLET_ACCESS_KEY" \
  https://mybtcpay.com/plugins/lnbank/api/lightning/info
```

This command should return the connection details as well as a JSON response containing your Lightning node information.

## Manual deployment

If you have deployed BTCPay Server using the [manual deployment](https://docs.btcpayserver.org/Deployment/ManualDeploymentExtended/), you might encounter the "LNbank requires an internal Lightning node to be configured" message on the LNbank wallets overview page.

In this case, make sure that you have configured the Lightning node you want to use as "internal node" using the `BTCPAY_BTCLIGHTNING` environment variable:

```bash
# set your Lightning node connection string
export BTCPAY_BTCLIGHTNING="type=lnd-rest;server=https://127.0.0.1:8080/;macaroonfilepath=/home/admin/.lnd/data/chain/bitcoin/mainnet/admin.macaroon"

# run the setup to apply the new setting
. ./btcpay-setup.sh -i
```

See the "Use custom node" view on the Lightning node connection setting screen in BTCPay Server for details on the connection string.
