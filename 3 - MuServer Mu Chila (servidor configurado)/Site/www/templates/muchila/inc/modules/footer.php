<?php
/** Mu Chila - rodapé do tema: marca, links, redes sociais (as que estiverem no webengine.json) e créditos. */
$mcSociais = array_filter(['discord' => config('social_link_discord', true), 'instagram' => config('social_link_instagram', true), 'facebook' => config('social_link_facebook', true)],
	function($u) { return check_value($u) && $u !== '#'; });
?>
<div class="mc-container">
	<div class="mc-footer__grid">
		<div>
			<a class="mc-brand" href="<?php echo __BASE_URL__; ?>"><span class="mc-brand__gem"></span><span class="mc-brand__name">MU CHILA</span></a>
			<p class="mc-footer__about">Servidor brasileiro de MU Online Season 14. Jogue com os amigos, evolua seu personagem e conquiste o castelo.</p>
			<?php if($mcSociais) { ?>
			<div class="mc-footer__social">
				<?php foreach($mcSociais as $rede => $url) echo '<a href="' . MuChilaUI::h($url) . '" target="_blank" rel="noopener" aria-label="' . ucfirst($rede) . '">' . MuChilaUI::icone($rede) . '</a>'; ?>
			</div>
			<?php } ?>
		</div>
		<div>
			<h5>Jogo</h5>
			<ul>
				<li><a href="<?php echo __BASE_URL__; ?>downloads/">Downloads</a></li>
				<li><a href="<?php echo __BASE_URL__; ?>info/"><?php echo lang('footer_info'); ?></a></li>
				<li><a href="<?php echo __BASE_URL__; ?>rankings/">Rankings</a></li>
				<li><a href="<?php echo __BASE_URL__; ?>usercp/lojaitens">Loja de itens</a></li>
			</ul>
		</div>
		<div>
			<h5>Suporte</h5>
			<ul>
				<li><a href="<?php echo __BASE_URL__; ?>tos/"><?php echo lang('footer_terms'); ?></a></li>
				<li><a href="<?php echo __BASE_URL__; ?>privacy/"><?php echo lang('footer_privacy'); ?></a></li>
				<li><a href="<?php echo __BASE_URL__; ?>refunds/"><?php echo lang('footer_refund'); ?></a></li>
				<li><a href="<?php echo __BASE_URL__; ?>contact/"><?php echo lang('footer_contact'); ?></a></li>
			</ul>
		</div>
	</div>
	<?php if(config('language_switch_active', true)) { ?><div style="margin-top:24px"><?php templateLanguageSelector(); ?></div><?php } ?>
	<div class="mc-footer__bottom">
		<p><?php echo langf('footer_copyright', array(config('server_name', true), date("Y"))); ?> <?php echo lang('footer_webzen_copyright'); ?></p>
		<?php $handler->webenginePowered(); ?>
	</div>
</div>
