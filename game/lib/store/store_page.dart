import 'package:flutter/material.dart';
import 'package:in_app_purchase/in_app_purchase.dart';
import '../ui/ambient_background.dart';
import '../ui/glass_panel.dart';
import 'diamonds.dart';
import 'store_service.dart';

class _PackInfo {
  final String id;
  final String title;
  final String subtitle;
  final IconData icon;
  final Color color;
  const _PackInfo(this.id, this.title, this.subtitle, this.icon, this.color);
}

const _diamondPacks = [
  _PackInfo(
    StoreIds.diamonds10,
    '10 Diamonds',
    'Unlock 1 level',
    Icons.diamond,
    kDiamondColor,
  ),
  _PackInfo(
    StoreIds.diamonds20,
    '20 Diamonds',
    'Unlock 2 levels',
    Icons.diamond,
    kDiamondColor,
  ),
  _PackInfo(
    StoreIds.diamonds30,
    '30 Diamonds',
    'Unlock 3 levels',
    Icons.diamond,
    kDiamondColor,
  ),
  _PackInfo(
    StoreIds.diamonds50,
    '50 Diamonds',
    'Unlock 5 levels',
    Icons.diamond,
    kDiamondColor,
  ),
  _PackInfo(
    StoreIds.diamonds100,
    '100 Diamonds',
    'Unlock 10 levels',
    Icons.diamond,
    kDiamondColor,
  ),
  _PackInfo(
    StoreIds.diamonds500,
    '500 Diamonds',
    'Best value',
    Icons.diamond,
    kDiamondColor,
  ),
];

const _hammerPacks = [
  _PackInfo(
    StoreIds.hammers10,
    '10 Hammers',
    'Smash any block in Block Puzzle',
    Icons.build,
    Colors.cyanAccent,
  ),
  _PackInfo(
    StoreIds.hammers20,
    '20 Hammers',
    'Smash 20 blocks',
    Icons.build,
    Colors.cyanAccent,
  ),
  _PackInfo(
    StoreIds.hammers30,
    '30 Hammers',
    'Smash 30 blocks',
    Icons.build,
    Colors.cyanAccent,
  ),
  _PackInfo(
    StoreIds.hammers50,
    '50 Hammers',
    'Smash 50 blocks',
    Icons.build,
    Colors.cyanAccent,
  ),
  _PackInfo(
    StoreIds.hammers100,
    '100 Hammers',
    'Best value',
    Icons.build,
    Colors.cyanAccent,
  ),
];

const _hintPacks = [
  _PackInfo(
    StoreIds.hints10,
    '10 Hints',
    'Reveal a Sudoku cell',
    Icons.lightbulb,
    Colors.amberAccent,
  ),
  _PackInfo(
    StoreIds.hints20,
    '20 Hints',
    'Reveal 20 cells',
    Icons.lightbulb,
    Colors.amberAccent,
  ),
  _PackInfo(
    StoreIds.hints30,
    '30 Hints',
    'Reveal 30 cells',
    Icons.lightbulb,
    Colors.amberAccent,
  ),
  _PackInfo(
    StoreIds.hints50,
    '50 Hints',
    'Reveal 50 cells',
    Icons.lightbulb,
    Colors.amberAccent,
  ),
  _PackInfo(
    StoreIds.hints100,
    '100 Hints',
    'Best value',
    Icons.lightbulb,
    Colors.amberAccent,
  ),
];

class StorePage extends StatelessWidget {
  const StorePage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        title: const Text(
          'Store',
          style: TextStyle(fontWeight: FontWeight.w900, letterSpacing: 1.5),
        ),
        backgroundColor: Colors.transparent,
        elevation: 0,
        foregroundColor: Colors.white,
      ),
      body: AmbientGlowBackground(
        gradientColors: const [Color(0xFF0F0C29), Color(0xFF302B63)],
        child: SafeArea(
          child: ListenableBuilder(
            listenable: StoreService.instance,
            builder: (context, _) => _buildBody(context, StoreService.instance),
          ),
        ),
      ),
    );
  }

  Widget _buildBody(BuildContext context, StoreService store) {
    return ListView(
      padding: const EdgeInsets.fromLTRB(20, 16, 20, 30),
      children: [
        _WalletBar(store: store),
        const SizedBox(height: 20),
        if (store.purchasePending)
          _notice(
            Icons.hourglass_top,
            Colors.amberAccent,
            'Payment pending. Your item will be added as soon as the payment '
            'is confirmed.',
          ),
        if (store.lastError != null)
          _notice(Icons.error_outline, Colors.redAccent, store.lastError!),
        ..._buildOffers(context, store),
      ],
    );
  }

  List<Widget> _buildOffers(BuildContext context, StoreService store) {
    if (!StoreService.platformSupported) {
      return [_message('Purchases are available in the Android and iOS apps.')];
    }
    if (store.loadingProducts) {
      return [
        const Padding(
          padding: EdgeInsets.all(40),
          child: Center(child: CircularProgressIndicator()),
        ),
      ];
    }
    // Every pack is always listed; one Google Play doesn't return (not
    // created in Play Console yet, or no connection) shows as unavailable.
    return [
      if (!store.storeAvailable || store.products.isEmpty) ...[
        _notice(
          Icons.cloud_off,
          Colors.orangeAccent,
          store.storeAvailable
              ? 'Prices could not be loaded from Google Play.'
              : 'Could not connect to the store. Check your connection and '
                    'that you are signed in to Google Play.',
        ),
        Center(
          child: TextButton.icon(
            onPressed: store.retry,
            icon: const Icon(Icons.refresh),
            label: const Text('Try again'),
          ),
        ),
        const SizedBox(height: 16),
      ],
      _PremiumCard(
        product: store.product(StoreIds.premium),
        owned: store.isPremium,
      ),
      const SizedBox(height: 26),
      ..._section(store, 'DIAMONDS', _diamondPacks),
      ..._section(store, 'HAMMERS', _hammerPacks),
      ..._section(store, 'SUDOKU HINTS', _hintPacks),
      if (store.storeAvailable) ...[
        const SizedBox(height: 10),
        Center(
          child: TextButton(
            onPressed: store.restore,
            child: const Text(
              'Restore Purchases',
              style: TextStyle(color: Colors.white70),
            ),
          ),
        ),
      ],
    ];
  }

  List<Widget> _section(
    StoreService store,
    String title,
    List<_PackInfo> packs,
  ) {
    return [
      Text(
        title,
        style: const TextStyle(
          color: Colors.white54,
          fontSize: 12,
          fontWeight: FontWeight.bold,
          letterSpacing: 1.5,
        ),
      ),
      const SizedBox(height: 10),
      for (final pack in packs) ...[
        _PackTile(info: pack, product: store.product(pack.id)),
        const SizedBox(height: 12),
      ],
      const SizedBox(height: 14),
    ];
  }

  Widget _notice(IconData icon, Color color, String text) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: GlassPanel(
        padding: const EdgeInsets.all(14),
        borderRadius: BorderRadius.circular(14),
        glowColor: color,
        glowOpacity: 0.15,
        child: Row(
          children: [
            Icon(icon, color: color),
            const SizedBox(width: 12),
            Expanded(
              child: Text(text, style: const TextStyle(color: Colors.white)),
            ),
          ],
        ),
      ),
    );
  }

  Widget _message(String text) {
    return Padding(
      padding: const EdgeInsets.only(top: 40),
      child: Text(
        text,
        textAlign: TextAlign.center,
        style: TextStyle(color: Colors.white.withValues(alpha: 0.75)),
      ),
    );
  }
}

class _WalletBar extends StatelessWidget {
  final StoreService store;
  const _WalletBar({required this.store});

  @override
  Widget build(BuildContext context) {
    Widget chip(IconData icon, Color color, String label) {
      return Expanded(
        child: GlassPanel(
          padding: const EdgeInsets.symmetric(vertical: 12),
          borderRadius: BorderRadius.circular(16),
          glowColor: color,
          glowOpacity: 0.15,
          blur: 8,
          child: Column(
            children: [
              Icon(icon, color: color, size: 22),
              const SizedBox(height: 4),
              Text(
                label,
                style: const TextStyle(
                  color: Colors.white,
                  fontWeight: FontWeight.w900,
                ),
              ),
            ],
          ),
        ),
      );
    }

    return Row(
      children: [
        chip(Icons.diamond, kDiamondColor, '${store.diamonds}'),
        const SizedBox(width: 12),
        chip(Icons.build, Colors.cyanAccent, '${store.hammers}'),
        const SizedBox(width: 12),
        chip(Icons.lightbulb, Colors.amberAccent, '${store.hints}'),
      ],
    );
  }
}

class _PremiumCard extends StatelessWidget {
  /// Null when Google Play didn't return the product.
  final ProductDetails? product;
  final bool owned;
  const _PremiumCard({required this.product, required this.owned});

  @override
  Widget build(BuildContext context) {
    final product = this.product;
    Widget perk(String text) => Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Row(
        children: [
          const Icon(Icons.check_circle, color: Colors.white, size: 18),
          const SizedBox(width: 8),
          Expanded(
            child: Text(
              text,
              style: const TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(24),
        gradient: const LinearGradient(
          colors: [Color(0xFFFF5FA2), Color(0xFF7B2FF7)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        boxShadow: [
          BoxShadow(
            color: Colors.pinkAccent.withValues(alpha: 0.4),
            blurRadius: 24,
            spreadRadius: 1,
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(
                Icons.workspace_premium,
                color: Colors.amberAccent,
                size: 30,
              ),
              SizedBox(width: 10),
              Text(
                'PREMIUM PACK',
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.w900,
                  letterSpacing: 1,
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),
          perk('Unlock all 8 premium Snake maps and every locked level'),
          perk('5 free hammers every Block Puzzle game (instead of 3)'),
          perk('5 free hints every Sudoku puzzle (instead of 3)'),
          perk('One-time purchase, yours forever'),
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: owned || product == null
                  ? null
                  : () => StoreService.instance.buy(product),
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.amberAccent,
                foregroundColor: Colors.black,
                disabledBackgroundColor: Colors.white24,
                disabledForegroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 14),
              ),
              child: Text(
                owned
                    ? 'OWNED'
                    : product == null
                    ? 'UNAVAILABLE'
                    : 'GET PREMIUM - ${product.price}',
                style: const TextStyle(fontWeight: FontWeight.w900),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _PackTile extends StatelessWidget {
  final _PackInfo info;

  /// Null when Google Play didn't return the product.
  final ProductDetails? product;
  const _PackTile({required this.info, required this.product});

  @override
  Widget build(BuildContext context) {
    final product = this.product;
    return GlassPanel(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      borderRadius: BorderRadius.circular(18),
      glowColor: info.color,
      glowOpacity: 0.15,
      blur: 8,
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: info.color.withValues(alpha: 0.18),
            ),
            child: Icon(info.icon, color: info.color),
          ),
          const SizedBox(width: 14),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  info.title,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 16,
                    fontWeight: FontWeight.w900,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  info.subtitle,
                  style: TextStyle(
                    color: Colors.white.withValues(alpha: 0.65),
                    fontSize: 12,
                  ),
                ),
              ],
            ),
          ),
          ElevatedButton(
            onPressed: product == null
                ? null
                : () => StoreService.instance.buy(product),
            style: ElevatedButton.styleFrom(
              backgroundColor: info.color,
              foregroundColor: Colors.black,
              disabledBackgroundColor: Colors.white12,
              disabledForegroundColor: Colors.white54,
            ),
            child: Text(
              product?.price ?? 'Unavailable',
              style: const TextStyle(fontWeight: FontWeight.w900),
            ),
          ),
        ],
      ),
    );
  }
}
